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

public class KeyAssignmentPersonDetailsViewModel
{
    public int PersonId { get; set; }
    public string KeyHolder { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? MinistryTeam { get; set; }
    public bool IsActivePerson { get; set; }
    public List<KeyAssignmentPersonKeyRow> Keys { get; set; } = new();

    public IEnumerable<KeyAssignmentPersonKeyRow> ActiveKeys => Keys.Where(k => k.ReturnedDate == null);
    public IEnumerable<KeyAssignmentPersonKeyRow> ReturnedKeys => Keys.Where(k => k.ReturnedDate != null);
    public DateTime? LastIssuedDate => Keys.Any() ? Keys.Max(k => k.IssuedDate) : null;
}

public class KeyAssignmentPersonKeyRow
{
    public int KeyAssignmentId { get; set; }
    public int KeyId { get; set; }
    public string KeyName { get; set; } = "";
    public string KeyCode { get; set; } = "";
    public DateTime IssuedDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public string Status { get; set; } = "";
    public string? ReasonIssued { get; set; }
    public string? Notes { get; set; }
}

public class KeyAssignmentPersonEditViewModel
{
    public int PersonId { get; set; }
    public string KeyHolder { get; set; } = "";
    public List<int> SelectedKeyIds { get; set; } = new();
    public string? Notes { get; set; }
    public List<KeyAssignmentEditKeyOption> Keys { get; set; } = new();
}

public class KeyAssignmentEditKeyOption
{
    public int KeyId { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsAssignedToThisPerson { get; set; }
    public bool IsAssignedToOtherPerson { get; set; }
    public string? AssignedToName { get; set; }
}
