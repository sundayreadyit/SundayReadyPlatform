using System.Security.Claims;
using ChurchAssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,PasswordVault")]
public class PasswordVaultController : Controller
{
    private readonly string _connectionString;
    private readonly IDataProtector _protector;

    public PasswordVaultController(IConfiguration configuration, IDataProtectionProvider dataProtectionProvider)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
        _protector = dataProtectionProvider.CreateProtector("CWCOperationsPortal.PasswordVault.v1");
    }

    private SqlConnection CreateConnection() => new(_connectionString);
    private int? CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public async Task<IActionResult> Index(string? search, string? category, bool includeInactive = false)
    {
        var model = new PasswordVaultIndexViewModel
        {
            Search = search,
            Category = category,
            IncludeInactive = includeInactive
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
SELECT CredentialId, Name, Category, Username, PasswordCipherText, Url, Owner, Notes,
       LastChangedDate, ExpirationDate, MfaEnabled, RecoveryEmail, Status, IsActive, CreatedDate, ModifiedDate
FROM dbo.PasswordVault
WHERE (@IncludeInactive = 1 OR IsActive = 1)
  AND (@Category IS NULL OR Category = @Category)
  AND (@Search IS NULL OR Name LIKE '%' + @Search + '%' OR Username LIKE '%' + @Search + '%' OR Url LIKE '%' + @Search + '%' OR Owner LIKE '%' + @Search + '%' OR Notes LIKE '%' + @Search + '%')
ORDER BY IsActive DESC, Category, Name;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);
        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(category) ? DBNull.Value : category.Trim());
        cmd.Parameters.AddWithValue("@Search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) model.Credentials.Add(ReadCredential(r));

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var credential = await GetByIdAsync(id);
        if (credential == null) return NotFound();
        return View(credential);
    }

    public IActionResult Create()
    {
        return View("Edit", new PasswordVaultRow
        {
            IsActive = true,
            Status = "Active",
            Category = "Other",
            LastChangedDate = DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PasswordVaultRow model)
    {
        ModelState.Remove(nameof(PasswordVaultRow.PasswordCipherText));
        ModelState.Remove(nameof(PasswordVaultRow.CreatedDate));
        ModelState.Remove(nameof(PasswordVaultRow.ModifiedDate));

        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password / Secret is required for new vault entries.");

        if (!ModelState.IsValid) return View("Edit", model);

        var cipherText = _protector.Protect(model.Password!.Trim());

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO dbo.PasswordVault
(Name, Category, Username, PasswordCipherText, Url, Owner, Notes, LastChangedDate, ExpirationDate, MfaEnabled, RecoveryEmail, Status, IsActive)
VALUES
(@Name, @Category, @Username, @PasswordCipherText, @Url, @Owner, @Notes, @LastChangedDate, @ExpirationDate, @MfaEnabled, @RecoveryEmail, @Status, @IsActive);
SELECT CONVERT(int, SCOPE_IDENTITY());";

        await using var cmd = BuildSaveCommand(sql, conn, model, cipherText);
        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await LogAuditAsync(conn, "Create", "PasswordVault", newId, $"Created vault entry: {model.Name}");

        TempData["SuccessMessage"] = "Password Vault entry created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var credential = await GetByIdAsync(id);
        if (credential == null) return NotFound();
        credential.Password = null;
        return View(credential);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PasswordVaultRow model)
    {
        if (id != model.CredentialId) return BadRequest();

        ModelState.Remove(nameof(PasswordVaultRow.PasswordCipherText));
        ModelState.Remove(nameof(PasswordVaultRow.CreatedDate));
        ModelState.Remove(nameof(PasswordVaultRow.ModifiedDate));

        if (!ModelState.IsValid) return View(model);

        var existing = await GetByIdAsync(id);
        if (existing == null) return NotFound();

        var cipherText = string.IsNullOrWhiteSpace(model.Password)
            ? existing.PasswordCipherText
            : _protector.Protect(model.Password.Trim());

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
UPDATE dbo.PasswordVault SET
    Name = @Name,
    Category = @Category,
    Username = @Username,
    PasswordCipherText = @PasswordCipherText,
    Url = @Url,
    Owner = @Owner,
    Notes = @Notes,
    LastChangedDate = @LastChangedDate,
    ExpirationDate = @ExpirationDate,
    MfaEnabled = @MfaEnabled,
    RecoveryEmail = @RecoveryEmail,
    Status = @Status,
    IsActive = @IsActive,
    ModifiedDate = SYSDATETIME()
WHERE CredentialId = @CredentialId;";

        await using var cmd = BuildSaveCommand(sql, conn, model, cipherText ?? "");
        cmd.Parameters.AddWithValue("@CredentialId", model.CredentialId);
        await cmd.ExecuteNonQueryAsync();
        await LogAuditAsync(conn, "Update", "PasswordVault", model.CredentialId, $"Updated vault entry: {model.Name}");

        TempData["SuccessMessage"] = "Password Vault entry updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.CredentialId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var credential = await GetByIdAsync(id);
        if (credential == null) return NotFound();

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.PasswordVault SET IsActive = 0, Status = 'Disabled', ModifiedDate = SYSDATETIME() WHERE CredentialId = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
        await LogAuditAsync(conn, "Deactivate", "PasswordVault", id, $"Deactivated vault entry: {credential.Name}");

        TempData["SuccessMessage"] = "Password Vault entry deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reveal(int id)
    {
        var credential = await GetByIdAsync(id);
        if (credential == null) return NotFound();

        var secret = "";
        try
        {
            secret = string.IsNullOrWhiteSpace(credential.PasswordCipherText) ? "" : _protector.Unprotect(credential.PasswordCipherText);
        }
        catch
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "The stored password could not be decrypted. The app data-protection key may have changed." });
        }

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await LogAuditAsync(conn, "Reveal", "PasswordVault", id, $"Revealed vault password for: {credential.Name}");

        return Json(new { success = true, password = secret });
    }

    private async Task<PasswordVaultRow?> GetByIdAsync(int id)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
SELECT CredentialId, Name, Category, Username, PasswordCipherText, Url, Owner, Notes,
       LastChangedDate, ExpirationDate, MfaEnabled, RecoveryEmail, Status, IsActive, CreatedDate, ModifiedDate
FROM dbo.PasswordVault
WHERE CredentialId = @Id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadCredential(r) : null;
    }

    private static PasswordVaultRow ReadCredential(SqlDataReader r) => new()
    {
        CredentialId = r.GetInt32(0),
        Name = r.GetString(1),
        Category = r.GetString(2),
        Username = r.IsDBNull(3) ? null : r.GetString(3),
        PasswordCipherText = r.IsDBNull(4) ? null : r.GetString(4),
        Url = r.IsDBNull(5) ? null : r.GetString(5),
        Owner = r.IsDBNull(6) ? null : r.GetString(6),
        Notes = r.IsDBNull(7) ? null : r.GetString(7),
        LastChangedDate = r.IsDBNull(8) ? null : r.GetDateTime(8),
        ExpirationDate = r.IsDBNull(9) ? null : r.GetDateTime(9),
        MfaEnabled = r.GetBoolean(10),
        RecoveryEmail = r.IsDBNull(11) ? null : r.GetString(11),
        Status = r.GetString(12),
        IsActive = r.GetBoolean(13),
        CreatedDate = r.GetDateTime(14),
        ModifiedDate = r.IsDBNull(15) ? null : r.GetDateTime(15)
    };

    private static SqlCommand BuildSaveCommand(string sql, SqlConnection conn, PasswordVaultRow model, string cipherText)
    {
        var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name", model.Name.Trim());
        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(model.Category) ? "Other" : model.Category.Trim());
        cmd.Parameters.AddWithValue("@Username", string.IsNullOrWhiteSpace(model.Username) ? DBNull.Value : model.Username.Trim());
        cmd.Parameters.AddWithValue("@PasswordCipherText", string.IsNullOrWhiteSpace(cipherText) ? DBNull.Value : cipherText);
        cmd.Parameters.AddWithValue("@Url", string.IsNullOrWhiteSpace(model.Url) ? DBNull.Value : model.Url.Trim());
        cmd.Parameters.AddWithValue("@Owner", string.IsNullOrWhiteSpace(model.Owner) ? DBNull.Value : model.Owner.Trim());
        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(model.Notes) ? DBNull.Value : model.Notes.Trim());
        cmd.Parameters.AddWithValue("@LastChangedDate", model.LastChangedDate.HasValue ? model.LastChangedDate.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ExpirationDate", model.ExpirationDate.HasValue ? model.ExpirationDate.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@MfaEnabled", model.MfaEnabled);
        cmd.Parameters.AddWithValue("@RecoveryEmail", string.IsNullOrWhiteSpace(model.RecoveryEmail) ? DBNull.Value : model.RecoveryEmail.Trim());
        cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status.Trim());
        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
        return cmd;
    }

    private async Task LogAuditAsync(SqlConnection conn, string actionType, string entityType, int? entityId, string description)
    {
        const string sql = @"INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
                             VALUES (@UserId, @ActionType, @EntityType, @EntityId, @Description);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", CurrentUserId.HasValue ? CurrentUserId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId.HasValue ? entityId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", description);
        await cmd.ExecuteNonQueryAsync();
    }
}
