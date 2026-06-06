namespace ChurchAssetTracker.Data;

public class ReservationCalendarUpdate1ViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = "";
    public DateTime FirstDayOfMonth { get; set; }
    public DateTime PreviousMonth { get; set; }
    public DateTime NextMonth { get; set; }
    public string Visibility { get; set; } = "All";
    public int? AccessAreaId { get; set; }
    public List<ReservationCalendarUpdate1Day> Days { get; set; } = new();
    public List<ReservationCalendarAreaOption> AccessAreas { get; set; } = new();
}

public class ReservationCalendarUpdate1Day
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public List<ReservationCalendarUpdate1Item> Reservations { get; set; } = new();
}

public class ReservationCalendarUpdate1Item
{
    public int ReservationId { get; set; }
    public string EventName { get; set; } = "";
    public string? AccessAreaName { get; set; }
    public string CalendarColor { get; set; } = "#475569";
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Status { get; set; } = "";
    public bool IsPublicEvent { get; set; }
}

public class ReservationCalendarAreaOption
{
    public int AccessAreaId { get; set; }
    public string AreaName { get; set; } = "";
    public string CalendarColor { get; set; } = "#475569";
}

public class ReservationDashboardSummary
{
    public int UpcomingApproved { get; set; }
    public int PendingApproval { get; set; }
    public int TodayReservations { get; set; }
    public int PublicUpcoming { get; set; }
    public List<ReservationMiniRow> UpcomingItems { get; set; } = new();
}

public class ReservationMiniRow
{
    public int ReservationId { get; set; }
    public string EventName { get; set; } = "";
    public string? AccessAreaName { get; set; }
    public DateTime StartDateTime { get; set; }
    public string Status { get; set; } = "";
    public bool IsPublicEvent { get; set; }
}