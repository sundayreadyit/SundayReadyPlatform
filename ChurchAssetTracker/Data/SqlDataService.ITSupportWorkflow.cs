using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task UpdateITSupportTicketStatusAsync(int ticketId, string status, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            UPDATE dbo.ITSupportTickets
            SET Status = @Status,
                UpdatedDate = SYSDATETIME(),
                ResolvedDate = CASE WHEN @Status = 'Resolved' AND ResolvedDate IS NULL THEN SYSDATETIME() ELSE ResolvedDate END,
                ClosedDate = CASE WHEN @Status IN ('Closed','Cancelled') AND ClosedDate IS NULL THEN SYSDATETIME() ELSE ClosedDate END
            WHERE TicketId = @TicketId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        cmd.Parameters.AddWithValue("@Status", status);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        await WriteITSupportWorkflowAuditLogAsync(username, "StatusChange", "ITSupportTicket", ticketId, $"IT support ticket status changed to {status}");
    }

    public async Task<string?> GetITSupportTicketAssignedUserEmailAsync(int ticketId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT u.Email
            FROM dbo.ITSupportTickets t
            INNER JOIN dbo.Users u ON t.AssignedToUserId = u.UserId
            WHERE t.TicketId = @TicketId
              AND u.IsActive = 1";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);

        await conn.OpenAsync();

        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value
            ? null
            : Convert.ToString(result);
    }

    public async Task<string?> GetUserEmailByUserIdAsync(int? userId)
    {
        if (!userId.HasValue)
            return null;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT Email
            FROM dbo.Users
            WHERE UserId = @UserId
              AND IsActive = 1";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId.Value);

        await conn.OpenAsync();

        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value
            ? null
            : Convert.ToString(result);
    }

    private async Task WriteITSupportWorkflowAuditLogAsync(string username, string actionType, string entityType, int entityId, string description)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
            SELECT TOP 1 UserId, @ActionType, @EntityType, @EntityId, @Description
            FROM dbo.Users
            WHERE Username = @Username
            UNION ALL
            SELECT NULL, @ActionType, @EntityType, @EntityId, @Description
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId);
        cmd.Parameters.AddWithValue("@Description", description);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }
}