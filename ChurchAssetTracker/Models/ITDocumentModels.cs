namespace ChurchAssetTracker.Models;

public class ITDocumentRow
{
    public int ITDocumentId { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedDate { get; set; }
    public bool IsActive { get; set; } = true;

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes >= 1024 * 1024)
                return $"{FileSizeBytes / 1024d / 1024d:0.0} MB";

            if (FileSizeBytes >= 1024)
                return $"{FileSizeBytes / 1024d:0.0} KB";

            return $"{FileSizeBytes} bytes";
        }
    }
}

public class ITDocumentLibraryViewModel
{
    public string Search { get; set; } = "";
    public string Category { get; set; } = "All";
    public List<ITDocumentRow> Documents { get; set; } = new();

    public List<string> Categories { get; set; } = new()
    {
        "Network Drawing",
        "Spreadsheet",
        "PDF / Manual",
        "Photo",
        "Vendor Document",
        "Configuration",
        "Procedure",
        "Other"
    };
}
