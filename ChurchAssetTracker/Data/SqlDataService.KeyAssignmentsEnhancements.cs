using ChurchAssetTracker.Models;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<KeyAssignmentPersonDetailsViewModel?> GetKeyAssignmentPersonDetailsAsync(int personId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string personSql = @"
            SELECT PersonId,
                   LTRIM(RTRIM(COALESCE(FirstName,'') + ' ' + COALESCE(LastName,''))) AS FullName,
                   Phone,
                   Email,
                   MinistryTeam,
                   IsActive
            FROM dbo.People
            WHERE PersonId = @PersonId;";

        var model = new KeyAssignmentPersonDetailsViewModel();
        await using (var cmd = new SqlCommand(personSql, conn))
        {
            cmd.Parameters.AddWithValue("@PersonId", personId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            model.PersonId = r.GetInt32(0);
            model.KeyHolder = r.GetString(1);
            model.Phone = r.IsDBNull(2) ? null : r.GetString(2);
            model.Email = r.IsDBNull(3) ? null : r.GetString(3);
            model.MinistryTeam = r.IsDBNull(4) ? null : r.GetString(4);
            model.IsActivePerson = r.GetBoolean(5);
        }

        const string keysSql = @"
            SELECT ka.KeyAssignmentId,
                   k.KeyId,
                   k.KeyName,
                   k.KeyCode,
                   ka.IssuedDate,
                   ka.ReturnedDate,
                   ka.Status,
                   ka.ReasonIssued,
                   ka.Notes
            FROM dbo.KeyAssignments ka
            JOIN dbo.Keys k ON ka.KeyId = k.KeyId
            WHERE ka.PersonId = @PersonId
            ORDER BY CASE WHEN ka.ReturnedDate IS NULL THEN 0 ELSE 1 END,
                     k.KeyName,
                     k.KeyCode;";

        await using (var cmd = new SqlCommand(keysSql, conn))
        {
            cmd.Parameters.AddWithValue("@PersonId", personId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                model.Keys.Add(new KeyAssignmentPersonKeyRow
                {
                    KeyAssignmentId = r.GetInt32(0),
                    KeyId = r.GetInt32(1),
                    KeyName = r.GetString(2),
                    KeyCode = r.GetString(3),
                    IssuedDate = r.GetDateTime(4),
                    ReturnedDate = r.IsDBNull(5) ? null : r.GetDateTime(5),
                    Status = r.GetString(6),
                    ReasonIssued = r.IsDBNull(7) ? null : r.GetString(7),
                    Notes = r.IsDBNull(8) ? null : r.GetString(8)
                });
            }
        }

        return model;
    }

    public async Task<KeyAssignmentPersonEditViewModel?> GetKeyAssignmentPersonEditAsync(int personId)
    {
        var details = await GetKeyAssignmentPersonDetailsAsync(personId);
        if (details == null) return null;

        var model = new KeyAssignmentPersonEditViewModel
        {
            PersonId = details.PersonId,
            KeyHolder = details.KeyHolder,
            SelectedKeyIds = details.ActiveKeys.Select(k => k.KeyId).ToList()
        };

        await using var conn = CreateConnection();
        const string sql = @"
            SELECT k.KeyId,
                   k.KeyCode + ' - ' + k.KeyName AS DisplayName,
                   CASE WHEN ka.PersonId = @PersonId THEN 1 ELSE 0 END AS AssignedToThisPerson,
                   CASE WHEN ka.PersonId IS NOT NULL AND ka.PersonId <> @PersonId THEN 1 ELSE 0 END AS AssignedToOtherPerson,
                   LTRIM(RTRIM(COALESCE(p.FirstName,'') + ' ' + COALESCE(p.LastName,''))) AS AssignedToName
            FROM dbo.Keys k
            LEFT JOIN dbo.KeyAssignments ka ON ka.KeyId = k.KeyId AND ka.ReturnedDate IS NULL
            LEFT JOIN dbo.People p ON p.PersonId = ka.PersonId
            WHERE k.IsActive = 1
            ORDER BY k.KeyName, k.KeyCode;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PersonId", personId);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            model.Keys.Add(new KeyAssignmentEditKeyOption
            {
                KeyId = r.GetInt32(0),
                DisplayName = r.GetString(1),
                IsAssignedToThisPerson = r.GetInt32(2) == 1,
                IsAssignedToOtherPerson = r.GetInt32(3) == 1,
                AssignedToName = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }

        return model;
    }

    public async Task UpdateKeyAssignmentsForPersonAsync(KeyAssignmentPersonEditViewModel model)
    {
        var selectedIds = (model.SelectedKeyIds ?? new List<int>()).Distinct().ToHashSet();

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        const string activeSql = @"
            SELECT KeyAssignmentId, KeyId
            FROM dbo.KeyAssignments
            WHERE PersonId = @PersonId
              AND ReturnedDate IS NULL;";

        var currentlyAssigned = new Dictionary<int, int>();
        await using (var cmd = new SqlCommand(activeSql, conn, (SqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@PersonId", model.PersonId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                currentlyAssigned[r.GetInt32(1)] = r.GetInt32(0);
        }

        foreach (var existing in currentlyAssigned)
        {
            if (selectedIds.Contains(existing.Key)) continue;

            const string returnSql = @"
                UPDATE dbo.KeyAssignments
                SET ReturnedDate = SYSDATETIME(),
                    Status = 'Returned',
                    Notes = CASE
                        WHEN @Notes IS NULL THEN Notes
                        WHEN Notes IS NULL OR LTRIM(RTRIM(Notes)) = '' THEN @Notes
                        ELSE Notes + CHAR(13) + CHAR(10) + 'Return Notes: ' + @Notes
                    END
                WHERE KeyAssignmentId = @KeyAssignmentId
                  AND ReturnedDate IS NULL;";

            await using var cmd = new SqlCommand(returnSql, conn, (SqlTransaction)tx);
            cmd.Parameters.AddWithValue("@KeyAssignmentId", existing.Value);
            cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(model.Notes) ? DBNull.Value : model.Notes.Trim());
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var keyId in selectedIds)
        {
            if (currentlyAssigned.ContainsKey(keyId)) continue;

            const string availableSql = @"
                SELECT COUNT(*)
                FROM dbo.Keys k
                WHERE k.KeyId = @KeyId
                  AND k.IsActive = 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.KeyAssignments ka
                      WHERE ka.KeyId = k.KeyId
                        AND ka.ReturnedDate IS NULL
                  );";

            await using (var cmd = new SqlCommand(availableSql, conn, (SqlTransaction)tx))
            {
                cmd.Parameters.AddWithValue("@KeyId", keyId);
                var available = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!available) continue;
            }

            const string insertSql = @"
                INSERT INTO dbo.KeyAssignments (PersonId, KeyId, IssuedDate, Notes, Status)
                VALUES (@PersonId, @KeyId, SYSDATETIME(), @Notes, 'Issued');";

            await using (var cmd = new SqlCommand(insertSql, conn, (SqlTransaction)tx))
            {
                cmd.Parameters.AddWithValue("@PersonId", model.PersonId);
                cmd.Parameters.AddWithValue("@KeyId", keyId);
                cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(model.Notes) ? DBNull.Value : model.Notes.Trim());
                await cmd.ExecuteNonQueryAsync();
            }
        }

        await tx.CommitAsync();
    }
}
