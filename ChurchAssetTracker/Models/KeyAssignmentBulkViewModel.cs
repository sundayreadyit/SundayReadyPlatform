namespace ChurchAssetTracker.Models;

public class KeyAssignmentBulkViewModel
{
    public int PersonId { get; set; }
    public List<int> SelectedKeyIds { get; set; } = new();
    public DateTime IssuedDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }

    public List<BulkKeyAssignmentPersonOption> People { get; set; } = new();
    public List<BulkKeyAssignmentKeyOption> Keys { get; set; } = new();
}

public class BulkKeyAssignmentPersonOption
{
    public int PersonId { get; set; }
    public string FullName { get; set; } = "";
}

public class BulkKeyAssignmentKeyOption
{
    public int KeyId { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsAlreadyAssignedToSelectedPerson { get; set; }
}