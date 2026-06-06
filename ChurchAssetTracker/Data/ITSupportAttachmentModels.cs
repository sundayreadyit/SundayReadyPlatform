namespace ChurchAssetTracker.Data;

public class ITSupportTicketAttachmentRow
{
    public int AttachmentId { get; set; }
    public int TicketId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public int? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedDate { get; set; }

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes >= 1024 * 1024)
                return $"{FileSizeBytes / 1024d / 1024d:0.##} MB";
            if (FileSizeBytes >= 1024)
                return $"{FileSizeBytes / 1024d:0.##} KB";
            return $"{FileSizeBytes} bytes";
        }
    }
}
