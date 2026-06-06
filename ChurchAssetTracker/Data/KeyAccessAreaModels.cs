namespace ChurchAssetTracker.Data;

public class AccessAreaRow
{
    public int AccessAreaId { get; set; }
    public string AreaName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class AccessAreaForm
{
    public int AccessAreaId { get; set; }
    public string AreaName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class KeyAccessManageViewModel
{
    public int KeyId { get; set; }
    public string KeyCode { get; set; } = "";
    public string KeyName { get; set; } = "";
    public List<AccessAreaRow> AllAccessAreas { get; set; } = new();
    public List<int> SelectedAccessAreaIds { get; set; } = new();
}