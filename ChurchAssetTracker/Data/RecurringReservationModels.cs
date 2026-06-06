namespace ChurchAssetTracker.Data;

public class RecurringReservationOccurrence
{
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
}

public class RecurringReservationCreateResult
{
    public int CreatedCount { get; set; }
    public List<ReservationConflictRow> Conflicts { get; set; } = new();
}