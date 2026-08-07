using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class WorshipSetSummary
{
    public int Id { get; set; }
    public DateTime ServiceDate { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public int SongCount { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public class WorshipSetItemModel
{
    public int Id { get; set; }
    public int WorshipSetId { get; set; }
    public int SortOrder { get; set; }
    public string SongTitle { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string KeyOverride { get; set; } = "";
    public string Leader { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class WorshipSetDetail
{
    public int Id { get; set; }
    public DateTime ServiceDate { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public List<WorshipSetItemModel> Items { get; set; } = new();
}

public class WorshipSetInputModel
{
    public int Id { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime ServiceDate { get; set; } = DateTime.Today.AddDays(((int)DayOfWeek.Sunday - (int)DateTime.Today.DayOfWeek + 7) % 7);

    [Required, MaxLength(200)]
    public string Title { get; set; } = "Sunday Morning Worship";

    [MaxLength(4000)]
    public string? Notes { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Draft";
}

public class WorshipSetEditorViewModel
{
    public WorshipSetDetail Set { get; set; } = new();
    public string Search { get; set; } = "";
    public List<WorshipSongRow> SearchResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class WorshipPlanningHomeViewModel : WorshipLibraryViewModel
{
    public List<WorshipSetSummary> UpcomingSets { get; set; } = new();
    public List<WorshipSetSummary> RecentSets { get; set; } = new();
}

public class WorshipSongUsage
{
    public string RelativePath { get; set; } = "";
    public int TimesUsed { get; set; }
    public DateTime? LastUsedDate { get; set; }
}
