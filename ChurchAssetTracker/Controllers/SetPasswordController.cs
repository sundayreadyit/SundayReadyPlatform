using System.Security.Cryptography;
using System.Text;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Controllers;

[AllowAnonymous]
public class SetPasswordController : Controller
{
    private readonly string _connectionString;

    public SetPasswordController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    [HttpGet]
    public async Task<IActionResult> Index(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction(nameof(Invalid));
        var tokenInfo = await GetValidTokenAsync(token);
        if (tokenInfo == null) return RedirectToAction(nameof(Invalid));
        ViewBag.Token = token;
        ViewBag.DisplayName = tokenInfo.Value.DisplayName;
        ViewBag.Username = tokenInfo.Value.Username;
        return View("~/Views/Account/SetPassword.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string token, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction(nameof(Invalid));
        var tokenInfo = await GetValidTokenAsync(token);
        if (tokenInfo == null) return RedirectToAction(nameof(Invalid));

        if (string.IsNullOrWhiteSpace(password)) ModelState.AddModelError(nameof(password), "Password is required.");
        if (password != confirmPassword) ModelState.AddModelError(nameof(confirmPassword), "Passwords do not match.");
        if (password?.Length < 8) ModelState.AddModelError(nameof(password), "Password must be at least 8 characters.");

        if (!ModelState.IsValid)
        {
            ViewBag.Token = token;
            ViewBag.DisplayName = tokenInfo.Value.DisplayName;
            ViewBag.Username = tokenInfo.Value.Username;
            return View("~/Views/Account/SetPassword.cshtml");
        }

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmd = new SqlCommand("UPDATE dbo.Users SET PasswordHash = @PasswordHash, IsActive = 1 WHERE UserId = @UserId;", conn, (SqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@UserId", tokenInfo.Value.UserId);
            cmd.Parameters.AddWithValue("@PasswordHash", AuthService.HashPassword(password));
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new SqlCommand("UPDATE dbo.UserPasswordSetupTokens SET UsedDate = SYSDATETIME() WHERE TokenId = @TokenId;", conn, (SqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@TokenId", tokenInfo.Value.TokenId);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new SqlCommand("INSERT INTO dbo.AuditLog (ActionType, EntityType, EntityId, Description) VALUES ('PasswordSetup', 'User', @UserId, @Description);", conn, (SqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@UserId", tokenInfo.Value.UserId);
            cmd.Parameters.AddWithValue("@Description", $"Password was set by user: {tokenInfo.Value.Username}");
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return RedirectToAction(nameof(Complete));
    }

    [HttpGet] public IActionResult Complete() => View("~/Views/Account/SetPasswordComplete.cshtml");
    [HttpGet] public IActionResult Invalid() => View("~/Views/Account/InvalidPasswordToken.cshtml");

    private async Task<TokenInfo?> GetValidTokenAsync(string token)
    {
        await using var conn = CreateConnection();
        const string sql = @"SELECT TOP 1 t.TokenId, u.UserId, u.Username, u.DisplayName
                             FROM dbo.UserPasswordSetupTokens t
                             INNER JOIN dbo.Users u ON t.UserId = u.UserId
                             WHERE t.TokenHash = @TokenHash
                               AND t.UsedDate IS NULL
                               AND t.ExpiresDate > SYSDATETIME()
                               AND u.IsActive = 1;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TokenHash", HashToken(token));
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new TokenInfo(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3));
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private readonly record struct TokenInfo(int TokenId, int UserId, string Username, string DisplayName);
}