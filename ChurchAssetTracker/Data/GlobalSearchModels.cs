namespace ChurchAssetTracker.Data;

public class GlobalSearchViewModel
{
    public string Query { get; set; } = "";
    public List<GlobalSearchResultGroup> Groups { get; set; } = new();

    public bool HasSearched => !string.IsNullOrWhiteSpace(Query);
    public bool HasResults => Groups.Any(g => g.Results.Any());
}

public class GlobalSearchResultGroup
{
    public string GroupName { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<GlobalSearchResultItem> Results { get; set; } = new();
}

public class GlobalSearchResultItem
{
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? Detail { get; set; }
    public string Url { get; set; } = "";
    public string Badge { get; set; } = "";
}
