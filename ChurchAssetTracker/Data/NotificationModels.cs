namespace ChurchAssetTracker.Data;

public class NotificationRow
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string NotificationType { get; set; } = "Info";
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ReadDate { get; set; }
}

public class NotificationCenterViewModel
{
    public int UnreadCount { get; set; }
    public List<NotificationRow> RecentNotifications { get; set; } = new();
}
