using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<ITSupportTechnicianDashboardViewModel> GetITSupportTechnicianDashboardAsync(int userId)
    {
        var model = new ITSupportTechnicianDashboardViewModel();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        model.TechnicianName = await GetTechnicianNameAsync(conn, userId);

        var allAssigned = await GetAssignedTicketRowsAsync(conn, userId);

        model.MyAssignedTickets = allAssigned
            .Where(t => !IsClosedLike(t.Status))
            .OrderByDescending(t => IsCritical(t.Priority))
            .ThenByDescending(t => IsOverdue(t))
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedDate)
            .Take(25)
            .ToList();

        model.OverdueTickets = allAssigned
            .Where(t => !IsClosedLike(t.Status) && IsOverdue(t))
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => IsCritical(t.Priority))
            .Take(25)
            .ToList();

        model.CriticalTickets = allAssigned
            .Where(t => !IsClosedLike(t.Status) && IsCritical(t.Priority))
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedDate)
            .Take(25)
            .ToList();

        model.WaitingOnUserTickets = allAssigned
            .Where(t => !IsClosedLike(t.Status) && string.Equals(t.Status, "Waiting on User", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedDate)
            .Take(25)
            .ToList();

        model.MyOpenTickets = allAssigned.Count(t => !IsClosedLike(t.Status));
        model.MyOverdueTickets = model.OverdueTickets.Count;
        model.MyCriticalTickets = model.CriticalTickets.Count;
        model.MyWaitingOnUserTickets = model.WaitingOnUserTickets.Count;

        return model;
    }

    private static async Task<string> GetTechnicianNameAsync(SqlConnection conn, int userId)
    {
        const string sql = @"
            SELECT DisplayName
            FROM dbo.Users
            WHERE UserId = @UserId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value
            ? "Technician"
            : Convert.ToString(result) ?? "Technician";
    }

    private static async Task<List<ITSupportTicketRow>> GetAssignedTicketRowsAsync(SqlConnection conn, int userId)
    {
        var list = new List<ITSupportTicketRow>();

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
            WHERE t.AssignedToUserId = @UserId
            ORDER BY t.CreatedDate DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new ITSupportTicketRow
            {
                TicketId = r.GetInt32(0),
                TicketNumber = r.IsDBNull(1) ? null : r.GetString(1),
                Title = r.GetString(2),
                Description = r.IsDBNull(3) ? null : r.GetString(3),
                Category = r.IsDBNull(4) ? null : r.GetString(4),
                Priority = r.GetString(5),
                Status = r.GetString(6),
                RequestedByPersonId = r.IsDBNull(7) ? null : r.GetInt32(7),
                RequestedByName = r.IsDBNull(8) ? null : r.GetString(8),
                RequestedByEmail = r.IsDBNull(9) ? null : r.GetString(9),
                RequestedByPhone = r.IsDBNull(10) ? null : r.GetString(10),
                AssignedToUserId = r.IsDBNull(11) ? null : r.GetInt32(11),
                AssignedToName = r.IsDBNull(12) ? null : r.GetString(12),
                ITAssetId = r.IsDBNull(13) ? null : r.GetInt32(13),
                ITAssetName = r.IsDBNull(14) ? null : r.GetString(14),
                AccessAreaId = r.IsDBNull(15) ? null : r.GetInt32(15),
                AccessAreaName = r.IsDBNull(16) ? null : r.GetString(16),
                DueDate = r.IsDBNull(17) ? null : r.GetDateTime(17),
                CreatedByName = r.IsDBNull(18) ? null : r.GetString(18),
                CreatedDate = r.GetDateTime(19),
                UpdatedDate = r.IsDBNull(20) ? null : r.GetDateTime(20),
                ResolvedDate = r.IsDBNull(21) ? null : r.GetDateTime(21),
                ClosedDate = r.IsDBNull(22) ? null : r.GetDateTime(22)
            });
        }

        return list;
    }

    private static bool IsClosedLike(string? status)
    {
        return string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCritical(string? priority)
    {
        return string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOverdue(ITSupportTicketRow ticket)
    {
        return ticket.DueDate.HasValue && ticket.DueDate.Value.Date < DateTime.Today;
    }
}