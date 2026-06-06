namespace ChurchAssetTracker.Models;

public class DashboardViewModel
{
    public int PeopleCount { get; set; }
    public int AssetCount { get; set; }
    public int CheckedOutItems { get; set; }
    public int OverdueItems { get; set; }
    public int KeysIssued { get; set; }
    public int LostKeys { get; set; }
    public List<ActivityItem> RecentActivity { get; set; } = new();
}

public class ActivityItem
{
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
