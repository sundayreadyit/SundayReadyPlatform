using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class PersonEditViewModel
{
    public int PersonId { get; set; }

    [Required(ErrorMessage = "First name is required")]
    [StringLength(100)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(255)]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; }

    [StringLength(100)]
    [Display(Name = "Ministry Team")]
    public string? MinistryTeam { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}
