namespace ChurchAssetTracker.Data;

public class ReservationCalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = "";
    public DateTime FirstDayOfMonth { get; set; }
    public DateTime PreviousMonth { get; set; }
    public DateTime NextMonth { get; set; }
    public List<ReservationCalendarDay> Days { get; set; } = new();
}

public class ReservationCalendarDay
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public List<ReservationCalendarItem> Reservations { get; set; } = new();
}

public class ReservationCalendarItem
{
    public int ReservationId { get; set; }
    public string EventName { get; set; } = "";
    public string? AccessAreaName { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Status { get; set; } = "";
    public bool IsPublicEvent { get; set; }
}