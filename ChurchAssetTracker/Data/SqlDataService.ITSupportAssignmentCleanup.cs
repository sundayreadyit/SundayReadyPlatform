using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<HashSet<int>> GetITAssignableUserIdsAsync()
    {
        var ids = new HashSet<int>();

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT DISTINCT u.UserId
            FROM dbo.Users u
            INNER JOIN dbo.UserRoles ur ON u.UserId = ur.UserId
            INNER JOIN dbo.Roles r ON ur.RoleId = r.RoleId
            WHERE u.IsActive = 1
              AND r.RoleName IN
              (
                  'ITAdmin',
                  'ITSupportManager',
                  'ITSupportTech',
                  'ITAssetManager'
              );";

        await using var cmd = new SqlCommand(sql, conn);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
            ids.Add(r.GetInt32(0));

        return ids;
    }

    public async Task<PersonContactInfo?> GetPersonContactInfoAsync(int personId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                FirstName + ' ' + LastName AS FullName,
                Email,
                Phone
            FROM dbo.People
            WHERE PersonId = @PersonId
              AND IsActive = 1;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PersonId", personId);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
            return null;

        return new PersonContactInfo
        {
            FullName = r.GetString(0),
            Email = r.IsDBNull(1) ? "" : r.GetString(1),
            Phone = r.IsDBNull(2) ? "" : r.GetString(2)
        };
    }
}

public class PersonContactInfo
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
}