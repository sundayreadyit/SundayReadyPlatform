using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class PasswordVaultRow
{
    public int CredentialId { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Required, StringLength(75)]
    public string Category { get; set; } = "Other";

    [StringLength(255)]
    public string? Username { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Password / Secret")]
    public string? Password { get; set; }

    public string? PasswordCipherText { get; set; }

    [StringLength(500)]
    [Display(Name = "URL")]
    public string? Url { get; set; }

    [StringLength(150)]
    public string? Owner { get; set; }

    public string? Notes { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Last Changed")]
    public DateTime? LastChangedDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Expiration Date")]
    public DateTime? ExpirationDate { get; set; }

    [Display(Name = "MFA Enabled")]
    public bool MfaEnabled { get; set; }

    [EmailAddress, StringLength(255)]
    [Display(Name = "Recovery Email")]
    public string? RecoveryEmail { get; set; }

    [Required, StringLength(50)]
    public string Status { get; set; } = "Active";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class PasswordVaultIndexViewModel
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public bool IncludeInactive { get; set; }
    public List<PasswordVaultRow> Credentials { get; set; } = new();

    public static readonly string[] Categories =
    {
        "Email Accounts",
        "Service Accounts",
        "Infrastructure",
        "Applications",
        "Network Devices",
        "Cloud Services",
        "Certificates",
        "Licenses",
        "WiFi",
        "Other"
    };

    public static readonly string[] Statuses =
    {
        "Active",
        "Disabled",
        "Expired",
        "Rotating",
        "Unknown"
    };
}
