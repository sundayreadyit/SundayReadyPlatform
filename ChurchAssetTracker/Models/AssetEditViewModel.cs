using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class AssetEditViewModel
{
    public int AssetId { get; set; }

    [Required]
    [Display(Name = "Asset Name")]
    public string AssetName { get; set; } = "";

    public string? Category { get; set; }

    [Display(Name = "Asset Tag")]
    public string? AssetTag { get; set; }

    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Asset Photo")]
    public string? PhotoPath { get; set; }

    [Range(1, 100000, ErrorMessage = "Quantity must be at least 1")]
    [Display(Name = "Total Quantity")]
    public int TotalQuantity { get; set; } = 1;

    [Display(Name = "Current Condition")]
    public string? CurrentCondition { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
