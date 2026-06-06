using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class ITAssetRow
{
    public int ITAssetId { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Asset Name")]
    public string AssetName { get; set; } = "";

    [StringLength(100)]
    [Display(Name = "Asset Type")]
    public string? AssetType { get; set; }

    [StringLength(100)]
    public string? Make { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(100)]
    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Asset Tag")]
    public string? AssetTag { get; set; }

    [StringLength(150)]
    [Display(Name = "Login Username")]
    public string? LoginUsername { get; set; }

    [StringLength(255)]
    [Display(Name = "Login Password")]
    public string? LoginPassword { get; set; }

    [StringLength(255)]
    [Display(Name = "Credential Reference")]
    public string? CredentialReference { get; set; }

    [StringLength(50)]
    [Display(Name = "IP Address")]
    public string? IPAddress { get; set; }

    [StringLength(50)]
    [Display(Name = "MAC Address")]
    public string? MACAddress { get; set; }

    [StringLength(150)]
    public string? Location { get; set; }

    [StringLength(150)]
    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    [StringLength(150)]
    [Display(Name = "Operating System")]
    public string? OperatingSystem { get; set; }

    [StringLength(100)]
    [Display(Name = "Firmware Version")]
    public string? FirmwareVersion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Purchase Date")]
    public DateTime? PurchaseDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Warranty Expiration")]
    public DateTime? WarrantyExpiration { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
