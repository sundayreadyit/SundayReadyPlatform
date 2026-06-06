using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Services;

public class AuthService
{
    private readonly string _connectionString;

    public AuthService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<AuthUser?> ValidateUserAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        await using var conn = CreateConnection();
        const string userSql = @"SELECT UserId, Username, DisplayName, PasswordHash, IsActive
                                 FROM dbo.Users
                                 WHERE Username = @Username";

        await using var cmd = new SqlCommand(userSql, conn);
        cmd.Parameters.AddWithValue("@Username", username.Trim());
        await conn.OpenAsync();

        int userId;
        string dbUsername;
        string displayName;
        string? storedHash;
        bool isActive;

        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync())
                return null;

            userId = r.GetInt32(0);
            dbUsername = r.GetString(1);
            displayName = r.GetString(2);
            storedHash = r.IsDBNull(3) ? null : r.GetString(3);
            isActive = r.GetBoolean(4);
        }

        if (!isActive || string.IsNullOrWhiteSpace(storedHash) || !VerifyPassword(password, storedHash))
            return null;

        var roles = new List<string>();
        const string roleSql = @"SELECT r.RoleName
                                 FROM dbo.UserRoles ur
                                 INNER JOIN dbo.Roles r ON ur.RoleId = r.RoleId
                                 WHERE ur.UserId = @UserId
                                 ORDER BY r.RoleName";
        await using var roleCmd = new SqlCommand(roleSql, conn);
        roleCmd.Parameters.AddWithValue("@UserId", userId);
        await using var rr = await roleCmd.ExecuteReaderAsync();
        while (await rr.ReadAsync())
            roles.Add(rr.GetString(0));

        return new AuthUser(userId, dbUsername, displayName, roles);
    }


    public static string HashPassword(string password)
    {
        const int iterations = 100000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256")
            return false;

        if (!int.TryParse(parts[1], out var iterations))
            return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

public record AuthUser(int UserId, string Username, string DisplayName, List<string> Roles);
