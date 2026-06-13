namespace ChurchAssetTracker.Data;

public class ITSupportTicketRow
{
    public int TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "New";
    public int? RequestedByPersonId { get; set; }
    public int? RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public string? RequestedByEmail { get; set; }
    public string? RequestedByPhone { get; set; }
    public int? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public int? ITAssetId { get; set; }
    public string? ITAssetName { get; set; }
    public int? AccessAreaId { get; set; }
    public string? AccessAreaName { get; set; }
    public DateTime? DueDate { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
}

public class ITSupportTicketForm
{
    public int TicketId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "New";

    public int? RequestedByPersonId { get; set; }
    public int? RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public string? RequestedByEmail { get; set; }
    public string? RequestedByPhone { get; set; }

    public int? AssignedToUserId { get; set; }
    public int? ITAssetId { get; set; }
    public int? AccessAreaId { get; set; }

    public DateTime? DueDate { get; set; }

    public List<PersonOption> People { get; set; } = new();
    public List<UserOption> RequesterUsers { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public List<ITAssetOption> ITAssets { get; set; } = new();
    public List<AccessAreaOption> AccessAreas { get; set; } = new();
}

public class ITSupportTicketCommentRow
{
    public int CommentId { get; set; }
    public int TicketId { get; set; }
    public string CommentText { get; set; } = "";
    public bool IsInternal { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ITSupportTicketDetailsViewModel
{
    public ITSupportTicketRow Ticket { get; set; } = new();
    public List<ITSupportTicketCommentRow> Comments { get; set; } = new();
    public ITSupportCommentForm NewComment { get; set; } = new();
}

public class ITSupportCommentForm
{
    public int TicketId { get; set; }
    public string CommentText { get; set; } = "";
    public bool IsInternal { get; set; }
}

public class UserOption
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class ITAssetOption
{
    public int ITAssetId { get; set; }
    public string DisplayName { get; set; } = "";
}

public class ITSupportDashboardSummary
{
    public int OpenTickets { get; set; }
    public int CriticalTickets { get; set; }
    public int WaitingOnUser { get; set; }
    public int ResolvedToday { get; set; }
}