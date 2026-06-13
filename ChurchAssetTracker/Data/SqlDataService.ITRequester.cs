using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<int?> GetUserPersonIdByUserIdAsync(int userId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP 1 p.PersonId
            FROM dbo.Users u
            INNER JOIN dbo.People p
                ON LOWER(LTRIM(RTRIM(u.Email))) = LOWER(LTRIM(RTRIM(p.Email)))
            WHERE u.UserId = @UserId
              AND p.IsActive = 1
              AND u.Email IS NOT NULL
              AND LTRIM(RTRIM(u.Email)) <> '';";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await conn.OpenAsync();

        var result = await cmd.ExecuteScalarAsync();

        return result == null || result == DBNull.Value
            ? null
            : Convert.ToInt32(result);
    }

    public async Task<List<ITSupportTicketRow>> GetITSupportTicketsForRequesterAsync(int userId)
    {
        var list = new List<ITSupportTicketRow>();

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT
                t.TicketId,
                t.TicketNumber,
                t.Title,
                t.Description,
                t.Category,
                t.Priority,
                t.Status,
                t.RequestedByPersonId,
                t.RequestedByUserId,
                t.RequestedByName,
                t.RequestedByEmail,
                t.RequestedByPhone,
                t.AssignedToUserId,
                assigned.DisplayName AS AssignedToName,
                t.ITAssetId,
                asset.AssetName AS ITAssetName,
                t.AccessAreaId,
                area.AreaName AS AccessAreaName,
                t.DueDate,
                created.DisplayName AS CreatedByName,
                t.CreatedDate,
                t.UpdatedDate,
                t.ResolvedDate,
                t.ClosedDate
            FROM dbo.ITSupportTickets t
            LEFT JOIN dbo.Users assigned ON t.AssignedToUserId = assigned.UserId
            LEFT JOIN dbo.Users created ON t.CreatedByUserId = created.UserId
            LEFT JOIN dbo.ITAssets asset ON t.ITAssetId = asset.ITAssetId
            LEFT JOIN dbo.AccessAreas area ON t.AccessAreaId = area.AccessAreaId
            WHERE
                t.CreatedByUserId = @UserId
                OR t.RequestedByUserId = @UserId
            ORDER BY t.CreatedDate DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(ReadTicketRow(r));
        }

        return list;
    }

    public async Task<bool> CanUserViewRequesterTicketAsync(int ticketId, int userId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT COUNT(*)
            FROM dbo.ITSupportTickets
            WHERE TicketId = @TicketId
              AND
              (
                  CreatedByUserId = @UserId
                  OR RequestedByUserId = @UserId
              );";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await conn.OpenAsync();

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<ITSupportTicketForm> BuildRequesterITSupportTicketFormAsync(int userId)
    {
        var model = await BuildITSupportTicketFormAsync();

        model.RequestedByUserId = userId;
        await ApplyRequesterUserContactAsync(model);

        model.Status = "New";
        model.Priority = "Medium";
        model.AssignedToUserId = null;

        return model;
    }

    private static ITSupportTicketRow ReadTicketRow(SqlDataReader r)
    {
        return new ITSupportTicketRow
        {
            TicketId = r.GetInt32(0),
            TicketNumber = r.IsDBNull(1) ? null : r.GetString(1),
            Title = r.GetString(2),
            Description = r.IsDBNull(3) ? null : r.GetString(3),
            Category = r.IsDBNull(4) ? null : r.GetString(4),
            Priority = r.GetString(5),
            Status = r.GetString(6),
            RequestedByPersonId = r.IsDBNull(7) ? null : r.GetInt32(7),
            RequestedByUserId = r.IsDBNull(8) ? null : r.GetInt32(8),
            RequestedByName = r.IsDBNull(9) ? null : r.GetString(9),
            RequestedByEmail = r.IsDBNull(10) ? null : r.GetString(10),
            RequestedByPhone = r.IsDBNull(11) ? null : r.GetString(11),
            AssignedToUserId = r.IsDBNull(12) ? null : r.GetInt32(12),
            AssignedToName = r.IsDBNull(13) ? null : r.GetString(13),
            ITAssetId = r.IsDBNull(14) ? null : r.GetInt32(14),
            ITAssetName = r.IsDBNull(15) ? null : r.GetString(15),
            AccessAreaId = r.IsDBNull(16) ? null : r.GetInt32(16),
            AccessAreaName = r.IsDBNull(17) ? null : r.GetString(17),
            DueDate = r.IsDBNull(18) ? null : r.GetDateTime(18),
            CreatedByName = r.IsDBNull(19) ? null : r.GetString(19),
            CreatedDate = r.GetDateTime(20),
            UpdatedDate = r.IsDBNull(21) ? null : r.GetDateTime(21),
            ResolvedDate = r.IsDBNull(22) ? null : r.GetDateTime(22),
            ClosedDate = r.IsDBNull(23) ? null : r.GetDateTime(23)
        };
    }
}