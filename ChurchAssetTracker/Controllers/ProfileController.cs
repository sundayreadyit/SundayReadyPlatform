using System.Security.Claims;
using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly string _connectionString;
    private readonly IWebHostEnvironment _environment;

    public ProfileController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
        _environment = environment;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    private int CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await GetCurrentUserProfileAsync();
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(UserProfileViewModel model, IFormFile? profilePicture)
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        if (string.IsNullOrWhiteSpace(model.DisplayName))
            ModelState.AddModelError(nameof(model.DisplayName), "Display name is required.");

        if (!ModelState.IsValid)
        {
            var existing = await GetCurrentUserProfileAsync();
            model.Username = existing?.Username ?? model.Username;
            model.ProfilePicturePath = existing?.ProfilePicturePath;
            return View(model);
        }

        string? newProfilePicturePath = null;

        try
        {
            if (profilePicture != null && profilePicture.Length > 0)
                newProfilePicturePath = await SaveProfilePictureAsync(profilePicture, userId);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            var existing = await GetCurrentUserProfileAsync();
            model.Username = existing?.Username ?? model.Username;
            model.ProfilePicturePath = existing?.ProfilePicturePath;
            return View(model);
        }

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = newProfilePicturePath == null
            ? "UPDATE dbo.Users SET DisplayName=@DisplayName, Email=@Email WHERE UserId=@UserId;"
            : "UPDATE dbo.Users SET DisplayName=@DisplayName, Email=@Email, ProfilePicturePath=@ProfilePicturePath WHERE UserId=@UserId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@DisplayName", model.DisplayName.Trim());
        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(model.Email) ? DBNull.Value : model.Email.Trim());
        if (newProfilePicturePath != null)
            cmd.Parameters.AddWithValue("@ProfilePicturePath", newProfilePicturePath);

        await cmd.ExecuteNonQueryAsync();

        await AddAuditAsync("Update", "UserProfile", userId, "Updated own user profile.");

        TempData["SuccessMessage"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProfilePicture()
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("UPDATE dbo.Users SET ProfilePicturePath=NULL WHERE UserId=@UserId;", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync();

        await AddAuditAsync("Update", "UserProfile", userId, "Removed own profile picture.");
        TempData["SuccessMessage"] = "Profile picture removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        if (!ModelState.IsValid)
            return View(model);

        string? currentHash;

        await using (var conn = CreateConnection())
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT PasswordHash FROM dbo.Users WHERE UserId=@UserId AND IsActive=1;", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cmd.ExecuteScalarAsync();
            currentHash = result == null || result == DBNull.Value ? null : Convert.ToString(result);
        }

        if (string.IsNullOrWhiteSpace(currentHash) || !AuthService.VerifyPassword(model.CurrentPassword, currentHash))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            return View(model);
        }

        await using (var conn = CreateConnection())
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("UPDATE dbo.Users SET PasswordHash=@PasswordHash WHERE UserId=@UserId;", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@PasswordHash", AuthService.HashPassword(model.NewPassword));
            await cmd.ExecuteNonQueryAsync();
        }

        await AddAuditAsync("ChangePassword", "User", userId, "User changed own password.");
        TempData["SuccessMessage"] = "Password changed successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<UserProfileViewModel?> GetCurrentUserProfileAsync()
    {
        var userId = CurrentUserId;
        if (userId <= 0) return null;

        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(
            "SELECT UserId, Username, DisplayName, Email, ProfilePicturePath FROM dbo.Users WHERE UserId=@UserId AND IsActive=1;", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new UserProfileViewModel
        {
            UserId = r.GetInt32(0),
            Username = r.GetString(1),
            DisplayName = r.GetString(2),
            Email = r.IsDBNull(3) ? null : r.GetString(3),
            ProfilePicturePath = r.IsDBNull(4) ? null : NormalizeProfilePath(r.GetString(4))
        };
    }

    private static string NormalizeProfilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        if (path.StartsWith("~/"))
            return path;

        if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return "~" + path;

        return path;
    }

    private async Task<string> SaveProfilePictureAsync(IFormFile file, int userId)
    {
        const long maxBytes = 2 * 1024 * 1024;
        if (file.Length > maxBytes)
            throw new InvalidOperationException("Profile picture must be 2 MB or smaller.");

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        var extension = Path.GetExtension(file.FileName);
        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Only JPG, PNG, GIF, and WEBP images are allowed.");

        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "profile-pictures");
        Directory.CreateDirectory(uploadRoot);

        var fileName = $"user-{userId}-{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadRoot, fileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        return $"~/uploads/profile-pictures/{fileName}";
    }

    private async Task AddAuditAsync(string actionType, string entityType, int? entityId, string description)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
                             VALUES (@UserId, @ActionType, @EntityType, @EntityId, @Description);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", CurrentUserId > 0 ? CurrentUserId : DBNull.Value);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId.HasValue ? entityId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", description);
        await cmd.ExecuteNonQueryAsync();
    }
}