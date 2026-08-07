using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class AdminUsersController : Controller
{
    private readonly string _connectionString;
    private readonly IEmailService _email;
    private readonly SystemSettingsService _settings;

    public AdminUsersController(IConfiguration configuration, IEmailService email, SystemSettingsService settings)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
        _email = email;
        _settings = settings;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<IActionResult> Index()
    {
        var users = new List<UserListItem>();
        await using var conn = CreateConnection();
        const string sql = @"SELECT u.UserId, u.Username, u.DisplayName, u.Email, u.IsActive, u.CreatedDate,
                   COALESCE(STRING_AGG(r.RoleName, ', '), '') AS Roles
            FROM dbo.Users u
            LEFT JOIN dbo.UserRoles ur ON u.UserId = ur.UserId
            LEFT JOIN dbo.Roles r ON ur.RoleId = r.RoleId
            GROUP BY u.UserId, u.Username, u.DisplayName, u.Email, u.IsActive, u.CreatedDate
            ORDER BY u.DisplayName;";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            users.Add(new UserListItem { UserId = r.GetInt32(0), Username = r.GetString(1), DisplayName = r.GetString(2), Email = r.IsDBNull(3) ? null : r.GetString(3), IsActive = r.GetBoolean(4), CreatedDate = r.GetDateTime(5), Roles = r.IsDBNull(6) ? "" : r.GetString(6) });
        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new UserEditViewModel { IsActive = true };
        await LoadRolesAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserEditViewModel model)
    {
        if (!ModelState.IsValid) { await LoadRolesAsync(model); return View(model); }

        try
        {
            int userId;
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            const string userSql = @"INSERT INTO dbo.Users (Username, DisplayName, Email, PasswordHash, IsActive)
                                     OUTPUT INSERTED.UserId
                                     VALUES (@Username, @DisplayName, @Email, @PasswordHash, @IsActive);";
            await using (var cmd = new SqlCommand(userSql, conn, (SqlTransaction)tx))
            {
                cmd.Parameters.AddWithValue("@Username", model.Username.Trim());
                cmd.Parameters.AddWithValue("@DisplayName", model.DisplayName.Trim());
                cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(model.Email) ? DBNull.Value : model.Email.Trim());
                cmd.Parameters.AddWithValue("@PasswordHash", string.IsNullOrWhiteSpace(model.Password) ? DBNull.Value : AuthService.HashPassword(model.Password));
                cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                userId = (int)await cmd.ExecuteScalarAsync();
            }

            await SaveRolesAsync(conn, (SqlTransaction)tx, userId, model.SelectedRoleIds);
            await AddAuditAsync(conn, (SqlTransaction)tx, "Create", "User", userId, $"Created user: {model.Username.Trim()}");
            await tx.CommitAsync();

            if (string.IsNullOrWhiteSpace(model.Password) && !string.IsNullOrWhiteSpace(model.Email))
            {
                await SendPasswordSetupInviteAsync(userId);
                TempData["SuccessMessage"] = "User created successfully and password setup invite was sent.";
            }
            else TempData["SuccessMessage"] = "User created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            ModelState.AddModelError(nameof(model.Username), "That username already exists.");
            await LoadRolesAsync(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await GetUserForEditAsync(id);
        if (model == null) return NotFound();
        await LoadRolesAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        if (!ModelState.IsValid) { await LoadRolesAsync(model); return View(model); }

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            var sql = string.IsNullOrWhiteSpace(model.Password)
                ? @"UPDATE dbo.Users SET Username=@Username, DisplayName=@DisplayName, Email=@Email, IsActive=@IsActive WHERE UserId=@UserId;"
                : @"UPDATE dbo.Users SET Username=@Username, DisplayName=@DisplayName, Email=@Email, IsActive=@IsActive, PasswordHash=@PasswordHash WHERE UserId=@UserId;";
            await using var cmd = new SqlCommand(sql, conn, (SqlTransaction)tx);
            cmd.Parameters.AddWithValue("@UserId", model.UserId);
            cmd.Parameters.AddWithValue("@Username", model.Username.Trim());
            cmd.Parameters.AddWithValue("@DisplayName", model.DisplayName.Trim());
            cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(model.Email) ? DBNull.Value : model.Email.Trim());
            cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
            if (!string.IsNullOrWhiteSpace(model.Password)) cmd.Parameters.AddWithValue("@PasswordHash", AuthService.HashPassword(model.Password));
            await cmd.ExecuteNonQueryAsync();

            await SaveRolesAsync(conn, (SqlTransaction)tx, model.UserId, model.SelectedRoleIds);
            await AddAuditAsync(conn, (SqlTransaction)tx, "Update", "User", model.UserId, $"Updated user: {model.Username.Trim()}");
            await tx.CommitAsync();
            TempData["SuccessMessage"] = "User updated successfully. Role changes take effect the next time that user logs in.";
            return RedirectToAction(nameof(Index));
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            ModelState.AddModelError(nameof(model.Username), "That username already exists.");
            await LoadRolesAsync(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> SendInvite(int id)
    {
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Invalid user selected for invite.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await SendPasswordSetupInviteAsync(id);
            TempData["SuccessMessage"] = "Password setup invite sent. A fallback copy was also sent to IT Support.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Invite could not be sent: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == id.ToString())
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account while logged in.";
            return RedirectToAction(nameof(Index));
        }
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @UserId;", conn);
        cmd.Parameters.AddWithValue("@UserId", id);
        await cmd.ExecuteNonQueryAsync();
        await AddAuditStandaloneAsync("Deactivate", "User", id, $"Deactivated user ID {id}");
        TempData["SuccessMessage"] = "User deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task SendPasswordSetupInviteAsync(int userId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        string username, displayName, email;

        await using (var cmd = new SqlCommand("SELECT Username, DisplayName, Email FROM dbo.Users WHERE UserId=@UserId AND IsActive=1;", conn))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();

            if (!await r.ReadAsync())
                throw new InvalidOperationException("Active user was not found.");

            username = r.GetString(0);
            displayName = r.GetString(1);
            email = r.IsDBNull(2) ? "" : r.GetString(2);
        }

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("User does not have an email address.");

        var rawToken = CreateSecureToken();
        var tokenHash = HashToken(rawToken);
        var expiresDate = DateTime.UtcNow.AddHours(72);

        await using (var cmd = new SqlCommand("UPDATE dbo.UserPasswordSetupTokens SET UsedDate=SYSDATETIME() WHERE UserId=@UserId AND UsedDate IS NULL;", conn))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new SqlCommand("INSERT INTO dbo.UserPasswordSetupTokens (UserId, TokenHash, ExpiresDate) VALUES (@UserId, @TokenHash, @ExpiresDate);", conn))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@TokenHash", tokenHash);
            cmd.Parameters.AddWithValue("@ExpiresDate", expiresDate);
            await cmd.ExecuteNonQueryAsync();
        }

        var setupUrl = Url.Action("Index", "SetPassword", new { token = rawToken }, Request.Scheme, Request.Host.ToString());

        if (string.IsNullOrWhiteSpace(setupUrl))
            throw new InvalidOperationException("Could not create password setup URL.");

        var branding = await _settings.GetBrandingAsync();
        var subject = $"Set your {branding.PortalName} password";

        var body =
$@"Hello {displayName},

An account has been created for you in {branding.PortalName}.

Username: {username}

Use the link below to set your password:
{setupUrl}

This link expires in 72 hours and can only be used once.

{branding.PortalName}";

        // Send the actual invite to the user's email address.
        await _email.SendEmailAsync(email.Trim(), subject, body);

        // Also send a fallback/admin visibility copy through the same route used by IT Support.
        // This confirms the invite was generated and gives IT a copy of the setup link if the user's mailbox filters it.
        await _email.SendITSupportEmailAsync(
            "User password setup invite generated",
$@"A password setup invite was generated in {branding.PortalName}.

User: {displayName}
Username: {username}
Email: {email}
Expires: {expiresDate:u}

Setup link:
{setupUrl}

If the user does not receive the email, IT/Admin can provide this link manually or resend the invite from User Administration.");

        await AddAuditStandaloneAsync("Invite", "User", userId, $"Sent password setup invite to {email} and fallback copy to IT Support");
    }

    private async Task<UserEditViewModel?> GetUserForEditAsync(int userId)
    {
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand("SELECT UserId, Username, DisplayName, Email, IsActive FROM dbo.Users WHERE UserId=@UserId;", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        var model = new UserEditViewModel { UserId = r.GetInt32(0), Username = r.GetString(1), DisplayName = r.GetString(2), Email = r.IsDBNull(3) ? null : r.GetString(3), IsActive = r.GetBoolean(4) };
        await r.CloseAsync();
        await using var roleCmd = new SqlCommand("SELECT RoleId FROM dbo.UserRoles WHERE UserId=@UserId;", conn);
        roleCmd.Parameters.AddWithValue("@UserId", userId);
        await using var rr = await roleCmd.ExecuteReaderAsync();
        while (await rr.ReadAsync()) model.SelectedRoleIds.Add(rr.GetInt32(0));
        return model;
    }

    private async Task LoadRolesAsync(UserEditViewModel model)
    {
        var selected = model.SelectedRoleIds.ToHashSet();
        model.AvailableRoles.Clear();
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand("SELECT RoleId, RoleName FROM dbo.Roles ORDER BY RoleName;", conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            model.AvailableRoles.Add(new RoleOption { RoleId = r.GetInt32(0), RoleName = r.GetString(1), IsSelected = selected.Contains(r.GetInt32(0)) });
    }

    private static async Task SaveRolesAsync(SqlConnection conn, SqlTransaction tx, int userId, IEnumerable<int> roleIds)
    {
        await using (var deleteCmd = new SqlCommand("DELETE FROM dbo.UserRoles WHERE UserId=@UserId;", conn, tx))
        {
            deleteCmd.Parameters.AddWithValue("@UserId", userId);
            await deleteCmd.ExecuteNonQueryAsync();
        }
        foreach (var roleId in roleIds.Distinct())
        {
            await using var insertCmd = new SqlCommand("INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);", conn, tx);
            insertCmd.Parameters.AddWithValue("@UserId", userId);
            insertCmd.Parameters.AddWithValue("@RoleId", roleId);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task AddAuditAsync(SqlConnection conn, SqlTransaction tx, string actionType, string entityType, int? entityId, string description)
    {
        await using var cmd = new SqlCommand("INSERT INTO dbo.AuditLog (ActionType, EntityType, EntityId, Description) VALUES (@ActionType, @EntityType, @EntityId, @Description);", conn, tx);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId.HasValue ? entityId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", description);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task AddAuditStandaloneAsync(string actionType, string entityType, int? entityId, string description)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await AddAuditAsync(conn, null!, actionType, entityType, entityId, description);
    }

    private static string CreateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace("+","-").Replace("/","_").Replace("=","");
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}