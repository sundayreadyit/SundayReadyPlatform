namespace ChurchAssetTracker.Data;

public class FacultyStaffDocumentRow
{
    public int FacultyStaffDocumentId { get; set; }
    public int FacultyStaffId { get; set; }
    public string DocumentType { get; set; } = "";
    public string DocumentTitle { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Notes { get; set; }
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

public class FacultyStaffEditViewModel
{
    public FacultyStaffRow FacultyStaff { get; set; } = new();
    public List<FacultyStaffDocumentRow> Documents { get; set; } = new();
}
