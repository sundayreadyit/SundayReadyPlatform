namespace ChurchAssetTracker.Models;

public class ITDocumentRow
{
    public int ITDocumentId { get; set; }
    public string DocumentNumber { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string DownloadToken { get; set; } = "";

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes >= 1024 * 1024) return $"{FileSizeBytes / 1024d / 1024d:0.0} MB";
            if (FileSizeBytes >= 1024) return $"{FileSizeBytes / 1024d:0.0} KB";
            return $"{FileSizeBytes} bytes";
        }
    }
}

public class ITDocumentLibraryViewModel
{
    public string Search { get; set; } = "";
    public string Category { get; set; } = "All";
    public string RootPath { get; set; } = "";
    public bool RootExists { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ITDocumentRow> Documents { get; set; } = new();

    public List<string> Categories { get; set; } = new()
    {
        "All", "IT Standards", "IT SOPs", "Disaster Recovery", "Infrastructure",
        "School Documents", "Enrollment Forms", "Church Documents", "Policies", "Forms", "Other"
    };
}
