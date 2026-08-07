using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class PortalBrandingSettings
{
    public string OrganizationName { get; set; } = "Christian Worship Center";
    public string OrganizationShortName { get; set; } = "CWC";
    public string PortalName { get; set; } = "CWC Operations Portal";
    public string PortalSubtitle { get; set; } = "Centralized church and school operations management";
    public string OrganizationWebsite { get; set; } = "";
    public string OrganizationEmail { get; set; } = "";
    public string OrganizationPhone { get; set; } = "";
    public string TimeZone { get; set; } = "Pacific Standard Time";
    public string LogoPath { get; set; } = "/images/branding/cwc-church-logo.png";
    public string FaviconPath { get; set; } = "/images/branding/cwc-app-icon-192.png";
    public string PrimaryColor { get; set; } = "#174c2f";
    public string SecondaryColor { get; set; } = "#14532d";
    public string AccentColor { get; set; } = "#16a34a";
}

public class PortalEmailSettings
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool PasswordConfigured { get; set; }
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "";
    public string AdminEmail { get; set; } = "";
    public string ITSupportEmail { get; set; } = "";
    public string ReservationsEmail { get; set; } = "";
}

public class PortalStorageSettings
{
    public string DocumentLibraryRootPath { get; set; } = @"\\CWCA-DC\Documentation";
    public string WorshipLibraryRootPath { get; set; } = @"\\CWCA-DC\Worship";
}

public class PortalModuleSetting
{
    public string ModuleKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool IsCore { get; set; }
}

public class AdministrationSettingsViewModel
{
    public PortalBrandingSettings Branding { get; set; } = new();
    public PortalEmailSettings Email { get; set; } = new();
    public PortalStorageSettings Storage { get; set; } = new();
    public List<PortalModuleSetting> Modules { get; set; } = new();
    public string TestEmailRecipient { get; set; } = "";
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BrandingSettingsInputModel
{
    [Required, MaxLength(200)] public string OrganizationName { get; set; } = "";
    [MaxLength(50)] public string OrganizationShortName { get; set; } = "";
    [Required, MaxLength(200)] public string PortalName { get; set; } = "";
    [MaxLength(300)] public string PortalSubtitle { get; set; } = "";
    [MaxLength(300)] public string OrganizationWebsite { get; set; } = "";
    [MaxLength(200)] public string OrganizationEmail { get; set; } = "";
    [MaxLength(100)] public string OrganizationPhone { get; set; } = "";
    [MaxLength(100)] public string TimeZone { get; set; } = "Pacific Standard Time";
    [Required, RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Primary color must be a 6-digit HEX color such as #174c2f.")] public string PrimaryColor { get; set; } = "#174c2f";
    [Required, RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Secondary color must be a 6-digit HEX color such as #14532d.")] public string SecondaryColor { get; set; } = "#14532d";
    [Required, RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Accent color must be a 6-digit HEX color such as #16a34a.")] public string AccentColor { get; set; } = "#16a34a";
}

public class EmailSettingsInputModel
{
    public bool Enabled { get; set; }
    [MaxLength(250)] public string SmtpHost { get; set; } = "";
    [Range(1,65535)] public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    [MaxLength(250)] public string Username { get; set; } = "";
    [DataType(DataType.Password)] public string Password { get; set; } = "";
    [EmailAddress] public string FromEmail { get; set; } = "";
    [MaxLength(200)] public string FromName { get; set; } = "";
    [EmailAddress] public string AdminEmail { get; set; } = "";
    [EmailAddress] public string ITSupportEmail { get; set; } = "";
    [EmailAddress] public string ReservationsEmail { get; set; } = "";
}

public class StorageSettingsInputModel
{
    [Required] public string DocumentLibraryRootPath { get; set; } = "";
    public string WorshipLibraryRootPath { get; set; } = "";
}

public class DashboardWidgetDefinition
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
}

public class DashboardBuilderViewModel
{
    public string Profile { get; set; } = "Default";
    public List<string> EnabledWidgetKeys { get; set; } = new();
    public List<DashboardWidgetDefinition> AvailableWidgets { get; set; } = new();
}
