using System.Net.Http.Json;
using System.Text.Json;
using ChurchAssetTracker.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Services;

public class LicenseService
{
    private readonly string _connectionString;
    private readonly HttpClient _httpClient;
    private readonly IDataProtector _protector;
    private readonly ILogger<LicenseService> _logger;
    private readonly string _apiBaseUrl;
    private readonly string _productCode;
    private readonly int _graceDays;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

    public LicenseService(IConfiguration configuration, IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider, ILogger<LicenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
        _httpClient = httpClientFactory.CreateClient("SundayReadyLicensing");
        _protector = dataProtectionProvider.CreateProtector("SundayReadyPlatform.LicenseKey.v1");
        _logger = logger;
        _apiBaseUrl = (configuration["Licensing:ApiBaseUrl"] ?? "").TrimEnd('/');
        _productCode = configuration["Licensing:ProductCode"] ?? "SRP";
        _graceDays = configuration.GetValue<int?>("Licensing:OfflineGraceDays") ?? 7;
    }

    public string ProductCode => _productCode;
    public string ApiBaseUrl => _apiBaseUrl;

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
IF OBJECT_ID('dbo.ApplicationLicense', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationLicense
    (
        Id int NOT NULL CONSTRAINT PK_ApplicationLicense PRIMARY KEY,
        ProtectedLicenseKey nvarchar(max) NULL,
        LicenseStatus nvarchar(50) NOT NULL CONSTRAINT DF_ApplicationLicense_Status DEFAULT('NotActivated'),
        CustomerName nvarchar(250) NULL,
        ProductName nvarchar(250) NULL,
        LicensedVersion nvarchar(50) NULL,
        ExpirationDate datetimeoffset NULL,
        LicensedModules nvarchar(max) NULL,
        LastValidatedUtc datetime2 NULL,
        UpdatedUtc datetime2 NOT NULL CONSTRAINT DF_ApplicationLicense_UpdatedUtc DEFAULT(SYSUTCDATETIME())
    );
END;
IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationLicense WHERE Id = 1)
    INSERT INTO dbo.ApplicationLicense (Id, LicenseStatus) VALUES (1, 'NotActivated');";
            await using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            _schemaReady = true;
        }
        finally { SchemaLock.Release(); }
    }

    public async Task<LicenseState> GetStateAsync()
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"SELECT ProtectedLicenseKey, LicenseStatus, CustomerName, ProductName, LicensedVersion,
ExpirationDate, LicensedModules, LastValidatedUtc FROM dbo.ApplicationLicense WHERE Id = 1";
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return new LicenseState();
        var hasKey = !r.IsDBNull(0) && !string.IsNullOrWhiteSpace(r.GetString(0));
        var status = r.IsDBNull(1) ? "NotActivated" : r.GetString(1);
        DateTime? last = r.IsDBNull(7) ? null : DateTime.SpecifyKind(r.GetDateTime(7), DateTimeKind.Utc);
        var graceEnd = last?.AddDays(_graceDays);
        var explicitInvalid = status.Equals("Revoked", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Expired", StringComparison.OrdinalIgnoreCase)
            || status.Equals("WrongProduct", StringComparison.OrdinalIgnoreCase)
            || status.Equals("NotFound", StringComparison.OrdinalIgnoreCase);
        return new LicenseState
        {
            IsActivated = hasKey,
            IsUsable = hasKey && !explicitInvalid && (status.Equals("Valid", StringComparison.OrdinalIgnoreCase) || (graceEnd.HasValue && graceEnd > DateTime.UtcNow)),
            Status = status,
            Customer = r.IsDBNull(2) ? null : r.GetString(2),
            Product = r.IsDBNull(3) ? null : r.GetString(3),
            LicensedVersion = r.IsDBNull(4) ? null : r.GetString(4),
            ExpirationDate = r.IsDBNull(5) ? null : r.GetDateTimeOffset(5).DateTime,
            LicensedModules = DeserializeModules(r.IsDBNull(6) ? null : r.GetString(6)),
            LastValidatedUtc = last,
            GracePeriodEndsUtc = graceEnd
        };
    }

    public async Task<LicenseState> ActivateAsync(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return new LicenseState { Status = "InvalidRequest", Message = "Enter a license key." };
        if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            return new LicenseState { Status = "ConfigurationError", Message = "The licensing API URL is not configured." };
        try
        {
            var response = await ValidateRemoteAsync(licenseKey.Trim());
            if (!response.Valid)
                return new LicenseState { Status = response.Status, Message = response.Message, ExpirationDate = response.ExpirationDate };
            await SaveAsync(licenseKey.Trim(), response, DateTime.UtcNow);
            var state = await GetStateAsync();
            state.Message = response.Message;
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to contact Sunday Ready licensing service during activation.");
            return new LicenseState { Status = "ValidationError", Message = "Unable to contact the Sunday Ready licensing service. Verify the API URL and try again." };
        }
    }

    public async Task<LicenseState> RevalidateAsync()
    {
        var key = await GetLicenseKeyAsync();
        if (string.IsNullOrWhiteSpace(key))
            return new LicenseState { Status = "NotActivated", Message = "No license has been activated." };
        try
        {
            var response = await ValidateRemoteAsync(key);
            if (response.Valid)
            {
                await SaveAsync(key, response, DateTime.UtcNow);
                var valid = await GetStateAsync(); valid.Message = response.Message; return valid;
            }
            await SaveStatusAsync(response.Status);
            var invalid = await GetStateAsync(); invalid.Message = response.Message; return invalid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to contact Sunday Ready licensing service; evaluating offline grace period.");
            var state = await GetStateAsync();
            state.Message = state.IsUsable
                ? $"Licensing service is unavailable. Offline grace period is active through {state.GracePeriodEndsUtc?.ToLocalTime():g}."
                : "Licensing service is unavailable and the offline grace period has ended.";
            return state;
        }
    }

    private async Task<LicenseValidationResponse> ValidateRemoteAsync(string key)
    {
        var payload = new { licenseKey = key, productCode = _productCode, version = PortalVersion.Version };
        using var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/license/validate", payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LicenseValidationResponse>()
            ?? throw new InvalidOperationException("Licensing service returned an empty response.");
    }

    private async Task<string?> GetLicenseKeyAsync()
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT ProtectedLicenseKey FROM dbo.ApplicationLicense WHERE Id=1", conn);
        var value = await cmd.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return _protector.Unprotect(value); }
        catch (Exception ex) { _logger.LogError(ex, "Stored license key could not be decrypted."); return null; }
    }

    private async Task SaveAsync(string key, LicenseValidationResponse response, DateTime validatedUtc)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
        const string sql = @"UPDATE dbo.ApplicationLicense SET ProtectedLicenseKey=@Key, LicenseStatus=@Status,
CustomerName=@Customer, ProductName=@Product, LicensedVersion=@Version, ExpirationDate=@Expiration,
LicensedModules=@Modules, LastValidatedUtc=@Validated, UpdatedUtc=SYSUTCDATETIME() WHERE Id=1";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Key", _protector.Protect(key));
        cmd.Parameters.AddWithValue("@Status", response.Status ?? "Valid");
        cmd.Parameters.AddWithValue("@Customer", (object?)response.Customer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Product", (object?)response.Product ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Version", (object?)response.LicensedVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Expiration", (object?)response.ExpirationDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Modules", JsonSerializer.Serialize(response.LicensedModules ?? new List<string>()));
        cmd.Parameters.AddWithValue("@Validated", validatedUtc);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SaveStatusAsync(string status)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.ApplicationLicense SET LicenseStatus=@Status, UpdatedUtc=SYSUTCDATETIME() WHERE Id=1", conn);
        cmd.Parameters.AddWithValue("@Status", status ?? "ValidationError"); await cmd.ExecuteNonQueryAsync();
    }

    private static List<string> DeserializeModules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); }
    }
}
