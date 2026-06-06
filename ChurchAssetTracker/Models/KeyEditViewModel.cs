using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class KeyEditViewModel
{
    public int KeyId { get; set; }

    [Required]
    [Display(Name = "Key Code")]
    [StringLength(100)]
    public string KeyCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Key Name")]
    [StringLength(150)]
    public string KeyName { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Master Key")]
    public bool IsMasterKey { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
