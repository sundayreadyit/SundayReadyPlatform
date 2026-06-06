using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class KeyAssignmentCreateViewModel
{
    [Required(ErrorMessage = "Key is required")]
    public int KeyId { get; set; }

    [Required(ErrorMessage = "Person is required")]
    public int PersonId { get; set; }

    [StringLength(255)]
    public string? ReasonIssued { get; set; }

    public string? Notes { get; set; }

    public List<OptionItem> AvailableKeys { get; set; } = new();
    public List<OptionItem> ActivePeople { get; set; } = new();
}

public class KeyAssignmentReturnViewModel
{
    public int KeyAssignmentId { get; set; }
    public string KeyName { get; set; } = "";
    public string KeyCode { get; set; } = "";
    public string KeyHolder { get; set; } = "";
    public DateTime IssuedDate { get; set; }
    public string? ReasonIssued { get; set; }
    public string? ExistingNotes { get; set; }

    public string? ReturnNotes { get; set; }
}
