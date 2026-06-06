using System.Security.Cryptography;
using System.Text;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Controllers;

[AllowAnonymous]
public class ForgotPasswordController : Controller
{
    private readonly string _connectionString;
    private readonly IEmailService _email;

    public ForgotPasswordController(IConfiguration configuration, IEmailService email)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");

        _email = email;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    [HttpGet]
    public IActionResult Index()
    {
        return View("~/Views/Account/ForgotPassword.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string usernameOrEmail)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail))
        {
            ViewBag.ErrorMessage = "Enter your username or email address.";
            return View("~/Views/Account/ForgotPassword.cshtml");
        }

        var user = await FindActiveUserAsync(usernameOrEmail.Trim());

        // Do not reveal whether an account exists.
        if (user == null)
        {
            ViewBag.SuccessMessage = "If an active account exists for that username or email, a password reset link has been sent.";
            return View("~/Views/Account/ForgotPassword.cshtml");
        }

        var rawToken = CreateSecureToken();
        var tokenHash = HashToken(rawToken);
        var expiresDate = DateTime.UtcNow.AddHours(2);

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using (var cmd = new SqlCommand("UPDATE dbo.UserPasswordSetupTokens SET UsedDate=SYSDATETIME() WHERE UserId=@UserId AND UsedDate IS NULL;", conn))
        {
            cmd.Parameters.AddWithValue("@UserId", user.Value.UserId);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new SqlCommand("INSERT INTO dbo.UserPasswordSetupTokens (UserId, TokenHash, ExpiresDate) VALUES (@UserId, @TokenHash, @ExpiresDate);", conn))
        {
            cmd.Parameters.AddWithValue("@UserId", user.Value.UserId);
            cmd.Parameters.AddWithValue("@TokenHash", tokenHash);
            cmd.Parameters.AddWithValue("@ExpiresDate", expiresDate);
            await cmd.ExecuteNonQueryAsync();
        }

        var resetUrl = Url.Action("Index", "SetPassword", new { token = rawToken }, Request.Scheme, Request.Host.ToString());

        if (string.IsNullOrWhiteSpace(resetUrl))
        {
            ViewBag.ErrorMessage = "Could not generate password reset link.";
            return View("~/Views/Account/ForgotPassword.cshtml");
        }

        var body =
$@"Hello {user.Value.DisplayName},

A password reset was requested for your CWC Operations Portal account.

Username: {user.Value.Username}

Use the link below to reset your password:
{resetUrl}

This link expires in 2 hours and can only be used once.

If you did not request this reset, you can ignore this email.

CWC Operations Portal";

        try
        {
            await _email.SendEmailAsync(user.Value.Email, "Reset your CWC Operations Portal password", body);

            await _email.SendITSupportEmailAsync(
                "Password reset link generated",
$@"A password reset link was generated in the CWC Operations Portal.

User: {user.Value.DisplayName}
Username: {user.Value.Username}
Email: {user.Value.Email}
Expires: {expiresDate:u}

Reset link:
{resetUrl}");

            ViewBag.SuccessMessage = "If an active account exists for that username or email, a password reset link has been sent.";
        }
        catch
        {
            ViewBag.ErrorMessage = "The password reset link could not be emailed. Please contact an administrator.";
        }

        return View("~/Views/Account/ForgotPassword.cshtml");
    }

    private async Task<(int UserId, string Username, string DisplayName, string Email)?> FindActiveUserAsync(string usernameOrEmail)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP 1 UserId, Username, DisplayName, Email
            FROM dbo.Users
            WHERE IsActive = 1
              AND Email IS NOT NULL
              AND LTRIM(RTRIM(Email)) <> ''
              AND (Username = @Value OR Email = @Value);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Value", usernameOrEmail);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
            return null;

        return (
            r.GetInt32(0),
            r.GetString(1),
            r.GetString(2),
            r.GetString(3)
        );
    }

    private static string CreateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
