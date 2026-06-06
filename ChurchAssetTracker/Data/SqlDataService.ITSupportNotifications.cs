using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<List<int>> GetITSupportManagerNotificationUserIdsAsync()
    {
        var ids = new List<int>();

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT DISTINCT u.UserId
            FROM dbo.Users u
            INNER JOIN dbo.UserRoles ur ON u.UserId = ur.UserId
            INNER JOIN dbo.Roles r ON ur.RoleId = r.RoleId
            WHERE u.IsActive = 1
              AND r.RoleName IN ('Admin', 'ITAdmin', 'ITSupportManager');";

        await using var cmd = new SqlCommand(sql, conn);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
            ids.Add(r.GetInt32(0));

        return ids;
    }

    public async Task<int?> GetITSupportTicketCreatedByUserIdAsync(int ticketId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT CreatedByUserId
            FROM dbo.ITSupportTickets
            WHERE TicketId = @TicketId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);

        await conn.OpenAsync();

        var result = await cmd.ExecuteScalarAsync();

        return result == null || result == DBNull.Value
            ? null
            : Convert.ToInt32(result);
    }

    public async Task<int?> GetITSupportTicketRequesterUserIdAsync(int ticketId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP 1 u.UserId
            FROM dbo.ITSupportTickets t
            INNER JOIN dbo.Users u
                ON LOWER(LTRIM(RTRIM(u.Email))) = LOWER(LTRIM(RTRIM(t.RequestedByEmail)))
            WHERE t.TicketId = @TicketId
              AND u.IsActive = 1
              AND t.RequestedByEmail IS NOT NULL
              AND LTRIM(RTRIM(t.RequestedByEmail)) <> '';";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);

        await conn.OpenAsync();

        var result = await cmd.ExecuteScalarAsync();

        return result == null || result == DBNull.Value
            ? null
            : Convert.ToInt32(result);
    }

    public async Task<List<int>> GetITSupportRequesterNotificationUserIdsAsync(int ticketId)
    {
        var ids = new HashSet<int>();

        var createdByUserId = await GetITSupportTicketCreatedByUserIdAsync(ticketId);
        if (createdByUserId.HasValue)
            ids.Add(createdByUserId.Value);

        var requesterUserId = await GetITSupportTicketRequesterUserIdAsync(ticketId);
        if (requesterUserId.HasValue)
            ids.Add(requesterUserId.Value);

        return ids.ToList();
    }

    public async Task NotifyUsersAsync(
        IEnumerable<int> userIds,
        string title,
        string message,
        string notificationType,
        string? entityType,
        int? entityId,
        string? linkUrl,
        int? excludeUserId = null)
    {
        var uniqueUserIds = userIds
            .Where(id => id > 0)
            .Distinct()
            .Where(id => !excludeUserId.HasValue || id != excludeUserId.Value)
            .ToList();

        foreach (var userId in uniqueUserIds)
        {
            await CreateNotificationAsync(
                userId,
                title,
                message,
                notificationType,
                entityType,
                entityId,
                linkUrl);
        }
    }

    public async Task NotifyITSupportManagersAsync(
        string title,
        string message,
        string notificationType,
        int ticketId,
        string linkUrl,
        int? excludeUserId = null)
    {
        var managerIds = await GetITSupportManagerNotificationUserIdsAsync();

        await NotifyUsersAsync(
            managerIds,
            title,
            message,
            notificationType,
            "ITSupportTicket",
            ticketId,
            linkUrl,
            excludeUserId);
    }

    public async Task NotifyITSupportAssignedUserAsync(
        int? assignedToUserId,
        string title,
        string message,
        string notificationType,
        int ticketId,
        string linkUrl,
        int? excludeUserId = null)
    {
        if (!assignedToUserId.HasValue)
            return;

        await NotifyUsersAsync(
            new[] { assignedToUserId.Value },
            title,
            message,
            notificationType,
            "ITSupportTicket",
            ticketId,
            linkUrl,
            excludeUserId);
    }

    public async Task NotifyITSupportRequesterAsync(
        int ticketId,
        string title,
        string message,
        string notificationType,
        string linkUrl,
        int? excludeUserId = null)
    {
        var requesterIds = await GetITSupportRequesterNotificationUserIdsAsync(ticketId);

        await NotifyUsersAsync(
            requesterIds,
            title,
            message,
            notificationType,
            "ITSupportTicket",
            ticketId,
            linkUrl,
            excludeUserId);
    }
}