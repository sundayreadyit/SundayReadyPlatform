namespace ChurchAssetTracker.Data;

public class DashboardEnhancedViewModel
{
    public DashboardReservationSummary ReservationSummary { get; set; } = new();
    public DashboardITSupportSummary ITSupportSummary { get; set; } = new();

    public List<DashboardUpcomingReservation> UpcomingReservations { get; set; } = new();
    public List<DashboardOverdueCheckout> OverdueCheckouts { get; set; } = new();
    public List<DashboardRecentAudit> RecentAuditItems { get; set; } = new();

    public List<StudentQuickSearchRow> StudentResults { get; set; } = new();
    public List<ITAssetQuickSearchRow> ITAssetResults { get; set; } = new();
}

public class DashboardReservationSummary
{
    public int UpcomingReservations { get; set; }
    public int PendingReservations { get; set; }
    public int ApprovedReservations { get; set; }
}

public class DashboardITSupportSummary
{
    public int OpenTickets { get; set; }
    public int CriticalTickets { get; set; }
    public int WaitingOnUser { get; set; }
}

public class DashboardUpcomingReservation
{
    public int ReservationId { get; set; }
    public string EventName { get; set; } = "";
    public string? AccessAreaName { get; set; }
    public DateTime StartDateTime { get; set; }
    public string Status { get; set; } = "";
}

public class DashboardOverdueCheckout
{
    public int CheckoutId { get; set; }
    public string AssetName { get; set; } = "";
    public string BorrowerName { get; set; } = "";
    public DateTime DueDate { get; set; }
}

public class DashboardRecentAudit
{
    public string? Username { get; set; }
    public string ActionType { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? Description { get; set; }
    public DateTime ActionDate { get; set; }
}

public class StudentQuickSearchRow
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = "";
    public string? GradeLevel { get; set; }
    public string? Classroom { get; set; }
}

public class ITAssetQuickSearchRow
{
    public int ITAssetId { get; set; }
    public string AssetName { get; set; } = "";
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? IPAddress { get; set; }
}