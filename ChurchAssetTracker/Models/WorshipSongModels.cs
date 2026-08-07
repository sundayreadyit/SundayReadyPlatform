namespace ChurchAssetTracker.Models;

public class WorshipSongRow
{
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Extension { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Token { get; set; } = "";
    public int TimesUsed { get; set; }
    public DateTime? LastUsedDate { get; set; }

    public string FileSizeDisplay => FileSizeBytes < 1024 * 1024
        ? $"{Math.Max(1, FileSizeBytes / 1024)} KB"
        : $"{FileSizeBytes / 1024d / 1024d:0.0} MB";
}

public class WorshipLibraryViewModel
{
    public string Search { get; set; } = "";
    public string RootPath { get; set; } = "";
    public bool RootExists { get; set; }
    public string? ErrorMessage { get; set; }
    public List<WorshipSongRow> Songs { get; set; } = new();
}
