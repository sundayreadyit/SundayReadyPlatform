using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ChurchAssetTracker.Models;
using System.Data;
using System.Security.Claims;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,ITAssetManager,ITAssetViewer")]
public class ITAssetsController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public ITAssetsController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
    }

    private bool CanEdit => User.IsInRole("Admin") || User.IsInRole("ITAssetManager");

    public async Task<IActionResult> Index(string? search, bool includeInactive = false)
    {
        var assets = new List<ITAssetRow>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
SELECT ITAssetId, AssetName, AssetType, Make, Model, SerialNumber, AssetTag, LoginUsername,
       LoginPassword, CredentialReference, IPAddress, MACAddress, Location, AssignedTo, OperatingSystem,
       FirmwareVersion, PurchaseDate, WarrantyExpiration, Notes, IsActive, CreatedDate, ModifiedDate
FROM dbo.ITAssets
WHERE (@IncludeInactive = 1 OR IsActive = 1)
  AND (@Search IS NULL OR AssetName LIKE '%' + @Search + '%' OR AssetType LIKE '%' + @Search + '%' OR Make LIKE '%' + @Search + '%' OR Model LIKE '%' + @Search + '%' OR IPAddress LIKE '%' + @Search + '%' OR Location LIKE '%' + @Search + '%' OR AssignedTo LIKE '%' + @Search + '%' OR FirmwareVersion LIKE '%' + @Search + '%')
ORDER BY IsActive DESC, AssetName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);
        cmd.Parameters.AddWithValue("@Search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            assets.Add(ReadITAsset(r));

        ViewBag.Search = search;
        ViewBag.IncludeInactive = includeInactive;
        ViewBag.CanEdit = CanEdit;
        return View(assets);
    }

    public async Task<IActionResult> Details(int id)
    {
        var asset = await GetByIdAsync(id);
        if (asset == null) return NotFound();
        ViewBag.CanEdit = CanEdit;
        return View(asset);
    }

    [Authorize(Roles = "Admin,ITAssetManager")]
    public IActionResult Create() => View("Edit", new ITAssetRow { IsActive = true });

    private bool ValidateITAssetForSave()
    {
        ModelState.Remove(nameof(ITAssetRow.CreatedDate));
        ModelState.Remove(nameof(ITAssetRow.ModifiedDate));
        ModelState.Remove("model.CreatedDate");
        ModelState.Remove("model.ModifiedDate");

        return ModelState.IsValid;
    }

    private string GetModelStateErrors()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        return errors.Any() ? string.Join(" | ", errors) : "Unknown validation issue.";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ITAssetManager")]
    public async Task<IActionResult> Create([Bind(Prefix = "")] ITAssetRow model)
    {
        model.LoginPassword = Request.Form["LoginPassword"].ToString();

        if (!ValidateITAssetForSave())
        {
            TempData["ErrorMessage"] = "IT Asset was not saved: " + GetModelStateErrors();
            return View("Edit", model);
        }

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO dbo.ITAssets
(AssetName, AssetType, Make, Model, SerialNumber, AssetTag, LoginUsername, LoginPassword, CredentialReference, IPAddress, MACAddress, Location, AssignedTo, OperatingSystem, FirmwareVersion, PurchaseDate, WarrantyExpiration, Notes, IsActive)
VALUES
(@AssetName, @AssetType, @Make, @Model, @SerialNumber, @AssetTag, @LoginUsername, @LoginPassword, @CredentialReference, @IPAddress, @MACAddress, @Location, @AssignedTo, @OperatingSystem, @FirmwareVersion, @PurchaseDate, @WarrantyExpiration, @Notes, @IsActive);
SELECT SCOPE_IDENTITY();";

        await using var cmd = BuildSaveCommand(sql, conn, model);
        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await WriteAuditAsync(conn, "Create", "ITAsset", newId, $"Created IT asset: {model.AssetName}");
        TempData["SuccessMessage"] = "IT Asset saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,ITAssetManager")]
    public async Task<IActionResult> Edit(int id)
    {
        var asset = await GetByIdAsync(id);
        if (asset == null) return NotFound();
        return View(asset);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ITAssetManager")]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "")] ITAssetRow model)
    {
        if (id != model.ITAssetId) return BadRequest();

        model.LoginPassword = Request.Form["LoginPassword"].ToString();

        if (!ValidateITAssetForSave())
        {
            TempData["ErrorMessage"] = "IT Asset was not saved: " + GetModelStateErrors();
            return View(model);
        }

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
UPDATE dbo.ITAssets SET
    AssetName = @AssetName,
    AssetType = @AssetType,
    Make = @Make,
    Model = @Model,
    SerialNumber = @SerialNumber,
    AssetTag = @AssetTag,
    LoginUsername = @LoginUsername,
    LoginPassword = @LoginPassword,
    CredentialReference = @CredentialReference,
    IPAddress = @IPAddress,
    MACAddress = @MACAddress,
    Location = @Location,
    AssignedTo = @AssignedTo,
    OperatingSystem = @OperatingSystem,
    FirmwareVersion = @FirmwareVersion,
    PurchaseDate = @PurchaseDate,
    WarrantyExpiration = @WarrantyExpiration,
    Notes = @Notes,
    IsActive = @IsActive,
    ModifiedDate = SYSDATETIME()
WHERE ITAssetId = @ITAssetId;";

        await using var cmd = BuildSaveCommand(sql, conn, model);
        cmd.Parameters.AddWithValue("@ITAssetId", model.ITAssetId);
        await cmd.ExecuteNonQueryAsync();
        await WriteAuditAsync(conn, "Update", "ITAsset", model.ITAssetId, $"Updated IT asset: {model.AssetName}");
        TempData["SuccessMessage"] = "IT Asset updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ITAssetManager")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var asset = await GetByIdAsync(id);
        if (asset == null) return NotFound();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.ITAssets SET IsActive = 0, ModifiedDate = SYSDATETIME() WHERE ITAssetId = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
        await WriteAuditAsync(conn, "Deactivate", "ITAsset", id, $"Deactivated IT asset: {asset.AssetName}");
        return RedirectToAction(nameof(Index));
    }

    private async Task<ITAssetRow?> GetByIdAsync(int id)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"
SELECT ITAssetId, AssetName, AssetType, Make, Model, SerialNumber, AssetTag, LoginUsername,
       LoginPassword, CredentialReference, IPAddress, MACAddress, Location, AssignedTo, OperatingSystem,
       FirmwareVersion, PurchaseDate, WarrantyExpiration, Notes, IsActive, CreatedDate, ModifiedDate
FROM dbo.ITAssets WHERE ITAssetId = @Id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadITAsset(r) : null;
    }

    private static ITAssetRow ReadITAsset(SqlDataReader r) => new()
    {
        ITAssetId = r.GetInt32(0),
        AssetName = r.GetString(1),
        AssetType = r.IsDBNull(2) ? null : r.GetString(2),
        Make = r.IsDBNull(3) ? null : r.GetString(3),
        Model = r.IsDBNull(4) ? null : r.GetString(4),
        SerialNumber = r.IsDBNull(5) ? null : r.GetString(5),
        AssetTag = r.IsDBNull(6) ? null : r.GetString(6),
        LoginUsername = r.IsDBNull(7) ? null : r.GetString(7),
        LoginPassword = r.IsDBNull(8) ? null : r.GetString(8),
        CredentialReference = r.IsDBNull(9) ? null : r.GetString(9),
        IPAddress = r.IsDBNull(10) ? null : r.GetString(10),
        MACAddress = r.IsDBNull(11) ? null : r.GetString(11),
        Location = r.IsDBNull(12) ? null : r.GetString(12),
        AssignedTo = r.IsDBNull(13) ? null : r.GetString(13),
        OperatingSystem = r.IsDBNull(14) ? null : r.GetString(14),
        FirmwareVersion = r.IsDBNull(15) ? null : r.GetString(15),
        PurchaseDate = r.IsDBNull(16) ? null : r.GetDateTime(16),
        WarrantyExpiration = r.IsDBNull(17) ? null : r.GetDateTime(17),
        Notes = r.IsDBNull(18) ? null : r.GetString(18),
        IsActive = r.GetBoolean(19),
        CreatedDate = r.GetDateTime(20),
        ModifiedDate = r.IsDBNull(21) ? null : r.GetDateTime(21)
    };

    private static SqlCommand BuildSaveCommand(string sql, SqlConnection conn, ITAssetRow model)
    {
        var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AssetName", model.AssetName.Trim());
        cmd.Parameters.AddWithValue("@AssetType", DbValue(model.AssetType));
        cmd.Parameters.AddWithValue("@Make", DbValue(model.Make));
        cmd.Parameters.AddWithValue("@Model", DbValue(model.Model));
        cmd.Parameters.AddWithValue("@SerialNumber", DbValue(model.SerialNumber));
        cmd.Parameters.AddWithValue("@AssetTag", DbValue(model.AssetTag));
        cmd.Parameters.AddWithValue("@LoginUsername", DbValue(model.LoginUsername));
        cmd.Parameters.AddWithValue("@LoginPassword", DbValue(model.LoginPassword));
        cmd.Parameters.AddWithValue("@CredentialReference", DbValue(model.CredentialReference));
        cmd.Parameters.AddWithValue("@IPAddress", DbValue(model.IPAddress));
        cmd.Parameters.AddWithValue("@MACAddress", DbValue(model.MACAddress));
        cmd.Parameters.AddWithValue("@Location", DbValue(model.Location));
        cmd.Parameters.AddWithValue("@AssignedTo", DbValue(model.AssignedTo));
        cmd.Parameters.AddWithValue("@OperatingSystem", DbValue(model.OperatingSystem));
        cmd.Parameters.AddWithValue("@FirmwareVersion", DbValue(model.FirmwareVersion));
        cmd.Parameters.AddWithValue("@PurchaseDate", model.PurchaseDate.HasValue ? model.PurchaseDate.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@WarrantyExpiration", model.WarrantyExpiration.HasValue ? model.WarrantyExpiration.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", DbValue(model.Notes));
        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
        return cmd;
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private async Task WriteAuditAsync(SqlConnection conn, string actionType, string entityType, int entityId, string description)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdClaim, out var parsed) ? parsed : null;

        const string sql = @"
INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
VALUES (@UserId, @ActionType, @EntityType, @EntityId, @Description);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId);
        cmd.Parameters.AddWithValue("@Description", description);
        await cmd.ExecuteNonQueryAsync();
    }
}
