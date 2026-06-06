using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<int> CreateNotificationAsync(int userId, string title, string message, string notificationType = "Info", string? entityType = null, int? entityId = null, string? linkUrl = null)
    {
        await using var conn = CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Notifications (UserId, Title, Message, NotificationType, EntityType, EntityId, LinkUrl)
            OUTPUT INSERTED.NotificationId
            VALUES (@UserId, @Title, @Message, @NotificationType, @EntityType, @EntityId, @LinkUrl);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Title", title.Trim());
        cmd.Parameters.AddWithValue("@Message", message.Trim());
        cmd.Parameters.AddWithValue("@NotificationType", string.IsNullOrWhiteSpace(notificationType) ? "Info" : notificationType.Trim());
        cmd.Parameters.AddWithValue("@EntityType", string.IsNullOrWhiteSpace(entityType) ? DBNull.Value : entityType.Trim());
        cmd.Parameters.AddWithValue("@EntityId", entityId.HasValue ? entityId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@LinkUrl", string.IsNullOrWhiteSpace(linkUrl) ? DBNull.Value : linkUrl.Trim());

        await conn.OpenAsync();
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> GetUnreadNotificationCountAsync(int userId)
    {
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=@UserId AND IsRead=0;", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await conn.OpenAsync();
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<NotificationRow>> GetRecentNotificationsAsync(int userId, int take = 8)
    {
        return await GetNotificationsInternalAsync(userId, false, take);
    }

    public async Task<List<NotificationRow>> GetNotificationsAsync(int userId, bool unreadOnly = false, int take = 100)
    {
        return await GetNotificationsInternalAsync(userId, unreadOnly, take);
    }

    private async Task<List<NotificationRow>> GetNotificationsInternalAsync(int userId, bool unreadOnly, int take)
    {
        var list = new List<NotificationRow>();
        await using var conn = CreateConnection();

        var sql = @"
            SELECT TOP (@Take) NotificationId, UserId, Title, Message, NotificationType, EntityType, EntityId, LinkUrl, IsRead, CreatedDate, ReadDate
            FROM dbo.Notifications
            WHERE UserId=@UserId";

        if (unreadOnly)
            sql += " AND IsRead=0";

        sql += " ORDER BY CreatedDate DESC, NotificationId DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Take", take);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(ReadNotificationRow(r));

        return list;
    }

    public async Task<NotificationRow?> GetNotificationAsync(int notificationId, int userId)
    {
        await using var conn = CreateConnection();
        const string sql = @"
            SELECT NotificationId, UserId, Title, Message, NotificationType, EntityType, EntityId, LinkUrl, IsRead, CreatedDate, ReadDate
            FROM dbo.Notifications
            WHERE NotificationId=@NotificationId AND UserId=@UserId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@NotificationId", notificationId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return ReadNotificationRow(r);
    }

    public async Task MarkNotificationReadAsync(int notificationId, int userId)
    {
        await using var conn = CreateConnection();
        const string sql = @"UPDATE dbo.Notifications SET IsRead=1, ReadDate=COALESCE(ReadDate, SYSDATETIME()) WHERE NotificationId=@NotificationId AND UserId=@UserId;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@NotificationId", notificationId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkAllNotificationsReadAsync(int userId)
    {
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand("UPDATE dbo.Notifications SET IsRead=1, ReadDate=COALESCE(ReadDate, SYSDATETIME()) WHERE UserId=@UserId AND IsRead=0;", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    private static NotificationRow ReadNotificationRow(SqlDataReader r)
    {
        return new NotificationRow
        {
            NotificationId = r.GetInt32(0),
            UserId = r.GetInt32(1),
            Title = r.GetString(2),
            Message = r.GetString(3),
            NotificationType = r.GetString(4),
            EntityType = r.IsDBNull(5) ? null : r.GetString(5),
            EntityId = r.IsDBNull(6) ? null : r.GetInt32(6),
            LinkUrl = r.IsDBNull(7) ? null : r.GetString(7),
            IsRead = r.GetBoolean(8),
            CreatedDate = r.GetDateTime(9),
            ReadDate = r.IsDBNull(10) ? null : r.GetDateTime(10)
        };
    }
}
