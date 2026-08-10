namespace ChurchAssetTracker.Models;

public class LicenseState
{
    public bool IsActivated { get; set; }
    public bool IsUsable { get; set; }
    public string Status { get; set; } = "NotActivated";
    public string? Message { get; set; }
    public string? Customer { get; set; }
    public string? Product { get; set; }
    public string? LicensedVersion { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? LastValidatedUtc { get; set; }
    public DateTime? GracePeriodEndsUtc { get; set; }
    public List<string> LicensedModules { get; set; } = new();
}

public class LicenseValidationResponse
{
    public bool Valid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Customer { get; set; }
    public string? Product { get; set; }
    public string? LicensedVersion { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<string> LicensedModules { get; set; } = new();
}

public class LicenseAdministrationViewModel
{
    public string LicenseKey { get; set; } = string.Empty;
    public LicenseState State { get; set; } = new();
    public string ProductCode { get; set; } = string.Empty;
    public string InstalledVersion { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
}
