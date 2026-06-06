using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.ViewComponents;

public class UserAvatarViewComponent : ViewComponent
{
    private readonly string _connectionString;

    public UserAvatarViewComponent(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new UserAvatarViewModel
        {
            DisplayName = User?.Identity?.Name ?? "User",
            ProfilePicturePath = null
        };

        var userIdValue = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
            return View(model);

        await using var conn = new SqlConnection(_connectionString);

        const string sql = @"
            SELECT DisplayName, ProfilePicturePath
            FROM dbo.Users
            WHERE UserId = @UserId
              AND IsActive = 1;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        if (await r.ReadAsync())
        {
            model.DisplayName = r.IsDBNull(0) ? model.DisplayName : r.GetString(0);
            model.ProfilePicturePath = r.IsDBNull(1) ? null : NormalizeProfilePath(r.GetString(1));
        }

        return View(model);
    }

    private static string? NormalizeProfilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (path.StartsWith("~/"))
            return path;

        if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return "~" + path;

        return path;
    }
}

public class UserAvatarViewModel
{
    public string DisplayName { get; set; } = "User";
    public string? ProfilePicturePath { get; set; }

    public string Initial
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
                return "U";

            return DisplayName.Trim().Substring(0, 1).ToUpperInvariant();
        }
    }
}