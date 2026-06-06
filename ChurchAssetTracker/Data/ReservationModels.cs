namespace ChurchAssetTracker.Data;

public class ReservationRow
{
    public int ReservationId { get; set; }
    public string EventName { get; set; } = "";
    public int? RequestedByPersonId { get; set; }
    public string? RequestedByName { get; set; }
    public int? AccessAreaId { get; set; }
    public string? AccessAreaName { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Purpose { get; set; }
    public string? SetupNotes { get; set; }
    public string? AccessKeyNeeds { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsPublicEvent { get; set; }
    public string? Notes { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime CreatedDate { get; set; }

    public Guid? RecurrenceGroupId { get; set; }
    public int? ParentReservationId { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
}

public class ReservationForm
{
    public int ReservationId { get; set; }
    public string EventName { get; set; } = "";
    public int? RequestedByPersonId { get; set; }
    public int? AccessAreaId { get; set; }
    public DateTime StartDateTime { get; set; } = DateTime.Today.AddDays(1).AddHours(9);
    public DateTime EndDateTime { get; set; } = DateTime.Today.AddDays(1).AddHours(10);
    public string Status { get; set; } = "Pending";
    public string? Purpose { get; set; }
    public string? SetupNotes { get; set; }
    public string? AccessKeyNeeds { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsPublicEvent { get; set; }
    public string? Notes { get; set; }

    public string RecurrencePattern { get; set; } = "None";
    public DateTime? RecurrenceEndDate { get; set; }

    public List<PersonOption> People { get; set; } = new();
    public List<AccessAreaOption> AccessAreas { get; set; } = new();
    public List<ReservationConflictRow> Conflicts { get; set; } = new();
}

public class PersonOption
{
    public int PersonId { get; set; }
    public string FullName { get; set; } = "";
}

public class AccessAreaOption
{
    public int AccessAreaId { get; set; }
    public string AreaName { get; set; } = "";
}

public class ReservationConflictRow
{
    public int ReservationId { get; set; }
    public string EventName { get; set; } = "";
    public string? AccessAreaName { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Status { get; set; } = "";
}