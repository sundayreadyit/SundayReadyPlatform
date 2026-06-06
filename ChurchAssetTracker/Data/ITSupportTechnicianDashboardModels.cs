namespace ChurchAssetTracker.Data;

public class ITSupportTechnicianDashboardViewModel
{
    public string TechnicianName { get; set; } = "";

    public int MyOpenTickets { get; set; }
    public int MyOverdueTickets { get; set; }
    public int MyCriticalTickets { get; set; }
    public int MyWaitingOnUserTickets { get; set; }

    public List<ITSupportTicketRow> MyAssignedTickets { get; set; } = new();
    public List<ITSupportTicketRow> OverdueTickets { get; set; } = new();
    public List<ITSupportTicketRow> CriticalTickets { get; set; } = new();
    public List<ITSupportTicketRow> WaitingOnUserTickets { get; set; } = new();
}