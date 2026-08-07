using ChurchAssetTracker.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Services;

public class SystemSettingsService
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;
    private readonly IDataProtector _protector;
    private readonly ILogger<SystemSettingsService> _logger;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

    private static readonly PortalModuleSetting[] ModuleCatalog =
    {
        new() { ModuleKey = "Reservations", DisplayName = "Reservations", Description = "Room and event reservations, calendar and approvals." },
        new() { ModuleKey = "Assets", DisplayName = "Assets & Checkouts", Description = "Church asset inventory and checkout tracking." },
        new() { ModuleKey = "Keys", DisplayName = "Keys & Access Areas", Description = "Keys, assignments and physical access areas." },
        new() { ModuleKey = "School", DisplayName = "School Directory", Description = "Students and faculty/staff records." },
        new() { ModuleKey = "ITAssets", DisplayName = "IT Assets", Description = "Technology inventory and infrastructure details." },
        new() { ModuleKey = "ITSupport", DisplayName = "IT Support", Description = "Support tickets, technician workflow and attachments." },
        new() { ModuleKey = "PasswordVault", DisplayName = "Password Vault", Description = "Restricted credential storage." },
        new() { ModuleKey = "Documentation", DisplayName = "Documentation Library", Description = "Search and download organizational documents." },
        new() { ModuleKey = "Worship", DisplayName = "Worship Song Library", Description = "Search, view and print licensed worship song files." },
        new() { ModuleKey = "People", DisplayName = "People", Description = "Central people directory.", IsCore = true },
        new() { ModuleKey = "Audit", DisplayName = "Audit Log", Description = "System activity history.", IsCore = true },
        new() { ModuleKey = "Notifications", DisplayName = "Notifications", Description = "Portal notifications.", IsCore = true },
        new() { ModuleKey = "Search", DisplayName = "Global Search", Description = "Cross-module search.", IsCore = true },
        new() { ModuleKey = "Administration", DisplayName = "Administration", Description = "Users, system settings and configuration.", IsCore = true }
    };

    public SystemSettingsService(
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SystemSettingsService> logger)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
        _protector = dataProtectionProvider.CreateProtector("SundayReadyPlatform.SystemSettings.v1");
        _logger = logger;
    }

    public async Task EnsureSchemaAsync()
    {
        if (_schemaReady) return;
        await SchemaLock.WaitAsync();
        try
        {
            if (_schemaReady) return;
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
IF OBJECT_ID('dbo.SystemSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemSettings
    (
        SettingKey nvarchar(200) NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
        SettingValue nvarchar(max) NULL,
        IsEncrypted bit NOT NULL CONSTRAINT DF_SystemSettings_IsEncrypted DEFAULT(0),
        UpdatedDate datetime2 NOT NULL CONSTRAINT DF_SystemSettings_UpdatedDate DEFAULT(SYSDATETIME())
    );
END;";
            await using (var cmd = new SqlCommand(sql, conn))
                await cmd.ExecuteNonQueryAsync();

            await SeedDefaultsAsync(conn);
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task SeedDefaultsAsync(SqlConnection conn)
    {
        var defaults = new Dictionary<string, (string Value, bool Encrypted)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Branding.OrganizationName"] = ("Christian Worship Center", false),
            ["Branding.OrganizationShortName"] = ("CWC", false),
            ["Branding.PortalName"] = ("CWC Operations Portal", false),
            ["Branding.PortalSubtitle"] = ("Centralized church and school operations management", false),
            ["Branding.OrganizationWebsite"] = ("", false),
            ["Branding.OrganizationEmail"] = ("", false),
            ["Branding.OrganizationPhone"] = ("", false),
            ["Branding.TimeZone"] = ("Pacific Standard Time", false),
            ["Branding.LogoPath"] = ("/images/branding/cwc-church-logo.png", false),
            ["Branding.FaviconPath"] = ("/images/branding/cwc-app-icon-192.png", false),
            ["Branding.PrimaryColor"] = ("#174c2f", false),
            ["Branding.SecondaryColor"] = ("#14532d", false),
            ["Branding.AccentColor"] = ("#16a34a", false),
            ["Storage.DocumentLibraryRootPath"] = (_configuration["DocumentLibrary:RootPath"] ?? @"\\CWCA-DC\Documentation", false),
            ["Storage.WorshipLibraryRootPath"] = (_configuration["WorshipLibrary:RootPath"] ?? @"\\CWCA-DC\Worship", false),
            ["Email.Enabled"] = ((_configuration.GetValue<bool?>("EmailSettings:Enabled") ?? false).ToString(), false),
            ["Email.SmtpHost"] = (_configuration["EmailSettings:SmtpHost"] ?? "", false),
            ["Email.SmtpPort"] = (_configuration["EmailSettings:SmtpPort"] ?? "587", false),
            ["Email.UseSsl"] = ((_configuration.GetValue<bool?>("EmailSettings:UseSsl") ?? true).ToString(), false),
            ["Email.Username"] = (_configuration["EmailSettings:Username"] ?? "", false),
            ["Email.FromEmail"] = (_configuration["EmailSettings:FromEmail"] ?? "", false),
            ["Email.FromName"] = (_configuration["EmailSettings:FromName"] ?? "CWC Operations Portal", false),
            ["Email.AdminEmail"] = (_configuration["EmailSettings:AdminEmail"] ?? "", false),
            ["Email.ITSupportEmail"] = (_configuration["EmailSettings:ITSupportEmail"] ?? "", false),
            ["Email.ReservationsEmail"] = (_configuration["EmailSettings:ReservationsEmail"] ?? "", false)
        };

        var existingPassword = _configuration["EmailSettings:Password"] ?? "";
        if (!string.IsNullOrWhiteSpace(existingPassword))
            defaults["Email.Password"] = (_protector.Protect(existingPassword), true);

        foreach (var module in ModuleCatalog)
            defaults[$"Module.{module.ModuleKey}"] = ("true", false);

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = @Key)
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, IsEncrypted) VALUES (@Key, @Value, @Encrypted);";

        foreach (var item in defaults)
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Key", item.Key);
            cmd.Parameters.AddWithValue("@Value", (object?)item.Value.Value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Encrypted", item.Value.Encrypted);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<Dictionary<string, (string Value, bool Encrypted)>> GetAllAsync()
    {
        try
        {
            await EnsureSchemaAsync();
            var result = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT SettingKey, SettingValue, IsEncrypted FROM dbo.SystemSettings", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result[r.GetString(0)] = (r.IsDBNull(1) ? "" : r.GetString(1), r.GetBoolean(2));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load database-backed portal settings. Falling back to application defaults.");
            return new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Get(Dictionary<string, (string Value, bool Encrypted)> values, string key, string fallback = "")
        => values.TryGetValue(key, out var item) ? item.Value : fallback;

    private static bool GetBool(Dictionary<string, (string Value, bool Encrypted)> values, string key, bool fallback)
        => bool.TryParse(Get(values, key), out var value) ? value : fallback;

    private static int GetInt(Dictionary<string, (string Value, bool Encrypted)> values, string key, int fallback)
        => int.TryParse(Get(values, key), out var value) ? value : fallback;

    public async Task<PortalBrandingSettings> GetBrandingAsync()
    {
        var v = await GetAllAsync();
        return new PortalBrandingSettings
        {
            OrganizationName = Get(v, "Branding.OrganizationName", "Christian Worship Center"),
            OrganizationShortName = Get(v, "Branding.OrganizationShortName", "CWC"),
            PortalName = Get(v, "Branding.PortalName", "CWC Operations Portal"),
            PortalSubtitle = Get(v, "Branding.PortalSubtitle", "Centralized church and school operations management"),
            OrganizationWebsite = Get(v, "Branding.OrganizationWebsite"),
            OrganizationEmail = Get(v, "Branding.OrganizationEmail"),
            OrganizationPhone = Get(v, "Branding.OrganizationPhone"),
            TimeZone = Get(v, "Branding.TimeZone", "Pacific Standard Time"),
            LogoPath = Get(v, "Branding.LogoPath", "/images/branding/cwc-church-logo.png"),
            FaviconPath = Get(v, "Branding.FaviconPath", "/images/branding/cwc-app-icon-192.png"),
            PrimaryColor = Get(v, "Branding.PrimaryColor", "#174c2f"),
            SecondaryColor = Get(v, "Branding.SecondaryColor", "#14532d"),
            AccentColor = Get(v, "Branding.AccentColor", "#16a34a")
        };
    }

    public async Task<PortalStorageSettings> GetStorageAsync()
    {
        var v = await GetAllAsync();
        return new PortalStorageSettings
        {
            DocumentLibraryRootPath = Get(v, "Storage.DocumentLibraryRootPath", _configuration["DocumentLibrary:RootPath"] ?? @"\\CWCA-DC\Documentation"),
            WorshipLibraryRootPath = Get(v, "Storage.WorshipLibraryRootPath", _configuration["WorshipLibrary:RootPath"] ?? @"\\CWCA-DC\Worship")
        };
    }

    public async Task<PortalEmailSettings> GetEmailAsync()
    {
        var v = await GetAllAsync();
        var password = "";
        var configured = v.TryGetValue("Email.Password", out var pwd) && !string.IsNullOrWhiteSpace(pwd.Value);
        if (configured)
        {
            try { password = pwd.Encrypted ? _protector.Unprotect(pwd.Value) : pwd.Value; }
            catch (Exception ex) { _logger.LogWarning(ex, "Stored SMTP password could not be decrypted."); }
        }

        return new PortalEmailSettings
        {
            Enabled = GetBool(v, "Email.Enabled", _configuration.GetValue<bool?>("EmailSettings:Enabled") ?? false),
            SmtpHost = Get(v, "Email.SmtpHost", _configuration["EmailSettings:SmtpHost"] ?? ""),
            SmtpPort = GetInt(v, "Email.SmtpPort", _configuration.GetValue<int?>("EmailSettings:SmtpPort") ?? 587),
            UseSsl = GetBool(v, "Email.UseSsl", _configuration.GetValue<bool?>("EmailSettings:UseSsl") ?? true),
            Username = Get(v, "Email.Username", _configuration["EmailSettings:Username"] ?? ""),
            Password = password,
            PasswordConfigured = configured,
            FromEmail = Get(v, "Email.FromEmail", _configuration["EmailSettings:FromEmail"] ?? ""),
            FromName = Get(v, "Email.FromName", _configuration["EmailSettings:FromName"] ?? "CWC Operations Portal"),
            AdminEmail = Get(v, "Email.AdminEmail", _configuration["EmailSettings:AdminEmail"] ?? ""),
            ITSupportEmail = Get(v, "Email.ITSupportEmail", _configuration["EmailSettings:ITSupportEmail"] ?? ""),
            ReservationsEmail = Get(v, "Email.ReservationsEmail", _configuration["EmailSettings:ReservationsEmail"] ?? "")
        };
    }

    public async Task<List<PortalModuleSetting>> GetModulesAsync()
    {
        var v = await GetAllAsync();
        return ModuleCatalog.Select(m => new PortalModuleSetting
        {
            ModuleKey = m.ModuleKey,
            DisplayName = m.DisplayName,
            Description = m.Description,
            IsCore = m.IsCore,
            IsEnabled = m.IsCore || GetBool(v, $"Module.{m.ModuleKey}", true)
        }).ToList();
    }

    public async Task<bool> IsModuleEnabledAsync(string moduleKey)
    {
        var modules = await GetModulesAsync();
        return modules.FirstOrDefault(x => x.ModuleKey.Equals(moduleKey, StringComparison.OrdinalIgnoreCase))?.IsEnabled ?? true;
    }

    public async Task SaveBrandingAsync(BrandingSettingsInputModel model, string? logoPath = null)
    {
        await SetAsync("Branding.OrganizationName", model.OrganizationName);
        await SetAsync("Branding.OrganizationShortName", model.OrganizationShortName);
        await SetAsync("Branding.PortalName", model.PortalName);
        await SetAsync("Branding.PortalSubtitle", model.PortalSubtitle);
        await SetAsync("Branding.OrganizationWebsite", model.OrganizationWebsite);
        await SetAsync("Branding.OrganizationEmail", model.OrganizationEmail);
        await SetAsync("Branding.OrganizationPhone", model.OrganizationPhone);
        await SetAsync("Branding.TimeZone", model.TimeZone);
        await SetAsync("Branding.PrimaryColor", model.PrimaryColor);
        await SetAsync("Branding.SecondaryColor", model.SecondaryColor);
        await SetAsync("Branding.AccentColor", model.AccentColor);
        if (!string.IsNullOrWhiteSpace(logoPath)) await SetAsync("Branding.LogoPath", logoPath);
    }

    public async Task SaveEmailAsync(EmailSettingsInputModel model)
    {
        await SetAsync("Email.Enabled", model.Enabled.ToString());
        await SetAsync("Email.SmtpHost", model.SmtpHost);
        await SetAsync("Email.SmtpPort", model.SmtpPort.ToString());
        await SetAsync("Email.UseSsl", model.UseSsl.ToString());
        await SetAsync("Email.Username", model.Username);
        if (!string.IsNullOrWhiteSpace(model.Password)) await SetAsync("Email.Password", _protector.Protect(model.Password), true);
        await SetAsync("Email.FromEmail", model.FromEmail);
        await SetAsync("Email.FromName", model.FromName);
        await SetAsync("Email.AdminEmail", model.AdminEmail);
        await SetAsync("Email.ITSupportEmail", model.ITSupportEmail);
        await SetAsync("Email.ReservationsEmail", model.ReservationsEmail);
    }

    public async Task SaveStorageAsync(StorageSettingsInputModel model)
    {
        await SetAsync("Storage.DocumentLibraryRootPath", model.DocumentLibraryRootPath);
        await SetAsync("Storage.WorshipLibraryRootPath", model.WorshipLibraryRootPath);
    }

    public async Task SaveModulesAsync(IEnumerable<string> enabledModuleKeys)
    {
        var enabled = new HashSet<string>(enabledModuleKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var module in ModuleCatalog.Where(m => !m.IsCore))
            await SetAsync($"Module.{module.ModuleKey}", enabled.Contains(module.ModuleKey).ToString());
    }

    private async Task SetAsync(string key, string? value, bool encrypted = false)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"
UPDATE dbo.SystemSettings SET SettingValue=@Value, IsEncrypted=@Encrypted, UpdatedDate=SYSDATETIME() WHERE SettingKey=@Key;
IF @@ROWCOUNT = 0 INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, IsEncrypted) VALUES (@Key,@Value,@Encrypted);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Key", key);
        cmd.Parameters.AddWithValue("@Value", (object?)value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Encrypted", encrypted);
        await cmd.ExecuteNonQueryAsync();
    }
    public static IReadOnlyList<DashboardWidgetDefinition> GetDashboardWidgetCatalog() => new List<DashboardWidgetDefinition>
    {
        new() { Key = "UpcomingReservationsMetric", DisplayName = "Upcoming Reservations Metric", Description = "Count of scheduled future reservations." },
        new() { Key = "PendingReservationsMetric", DisplayName = "Pending Reservations Metric", Description = "Reservations awaiting approval." },
        new() { Key = "OpenTicketsMetric", DisplayName = "Open IT Tickets Metric", Description = "Count of active IT support requests." },
        new() { Key = "CriticalTicketsMetric", DisplayName = "Critical Tickets Metric", Description = "Count of critical/high urgency IT tickets." },
        new() { Key = "UpcomingReservationsPanel", DisplayName = "Upcoming Reservations", Description = "Detailed list of upcoming events." },
        new() { Key = "OverdueAssetsPanel", DisplayName = "Overdue Assets", Description = "Borrowed assets past their return date." },
        new() { Key = "StudentSearchPanel", DisplayName = "Student Quick Search", Description = "Quick student directory lookup." },
        new() { Key = "ITAssetSearchPanel", DisplayName = "IT Asset Quick Search", Description = "Quick technology inventory lookup." },
        new() { Key = "RecentActivityPanel", DisplayName = "Recent Activity", Description = "Recent audit log activity." }
    };

    public async Task<List<string>> GetDashboardLayoutAsync(string profile)
    {
        var catalog = GetDashboardWidgetCatalog();
        var defaults = catalog.Select(x => x.Key).ToList();
        var v = await GetAllAsync();
        var settingKey = $"Dashboard.{NormalizeDashboardProfile(profile)}";
        var hasSavedLayout = v.TryGetValue(settingKey, out var saved);
        var raw = hasSavedLayout ? saved.Value : string.Join(',', defaults);
        var valid = catalog.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(valid.Contains).ToList();
        return hasSavedLayout ? result : defaults;
    }

    public async Task SaveDashboardLayoutAsync(string profile, IEnumerable<string> widgetKeys)
    {
        var valid = GetDashboardWidgetCatalog().Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keys = (widgetKeys ?? Array.Empty<string>()).Where(valid.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        await SetAsync($"Dashboard.{NormalizeDashboardProfile(profile)}", string.Join(',', keys));
    }

    private static string NormalizeDashboardProfile(string profile)
    {
        var value = (profile ?? "Default").Trim();
        return value.ToLowerInvariant() switch
        {
            "admin" => "Admin",
            "it" => "IT",
            "school" => "School",
            "church" => "Church",
            _ => "Default"
        };
    }

}
