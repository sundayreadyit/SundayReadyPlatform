using System.Security.Claims;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class AdministrationController : Controller
{
    private readonly SystemSettingsService _settings;
    private readonly IEmailService _email;
    private readonly SqlDataService _data;
    private readonly IWebHostEnvironment _environment;

    public AdministrationController(SystemSettingsService settings, IEmailService email, SqlDataService data, IWebHostEnvironment environment)
    {
        _settings = settings;
        _email = email;
        _data = data;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new AdministrationSettingsViewModel
        {
            Branding = await _settings.GetBrandingAsync(),
            Email = await _settings.GetEmailAsync(),
            Storage = await _settings.GetStorageAsync(),
            Modules = await _settings.GetModulesAsync(),
            StatusMessage = TempData["AdminSettingsMessage"] as string,
            ErrorMessage = TempData["AdminSettingsError"] as string
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGeneral(BrandingSettingsInputModel model, IFormFile? organizationLogo)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminSettingsError"] = "Organization settings were not saved. Check the entered values.";
            return RedirectToAction(nameof(Index), new { section = "general" });
        }

        string? logoPath = null;
        if (organizationLogo is { Length: > 0 })
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
            var ext = Path.GetExtension(organizationLogo.FileName);
            if (!allowed.Contains(ext) || organizationLogo.Length > 5 * 1024 * 1024)
            {
                TempData["AdminSettingsError"] = "Logo must be PNG, JPG, JPEG or WEBP and no larger than 5 MB.";
                return RedirectToAction(nameof(Index), new { section = "general" });
            }

            var folder = Path.Combine(_environment.WebRootPath, "uploads", "branding");
            Directory.CreateDirectory(folder);
            var fileName = $"organization-logo{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(fullPath);
            await organizationLogo.CopyToAsync(stream);
            logoPath = $"/uploads/branding/{fileName}";
        }

        await _settings.SaveBrandingAsync(model, logoPath);
        await LogAsync("Update", "SystemSettings", "Updated organization branding and general portal settings.");
        TempData["AdminSettingsMessage"] = "Organization settings saved. Branding changes are active immediately.";
        return RedirectToAction(nameof(Index), new { section = "general" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmail(EmailSettingsInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminSettingsError"] = "Email settings were not saved. Check the entered values.";
            return RedirectToAction(nameof(Index), new { section = "email" });
        }
        await _settings.SaveEmailAsync(model);
        await LogAsync("Update", "SystemSettings", "Updated SMTP/email settings. SMTP password value was not written to the audit log.");
        TempData["AdminSettingsMessage"] = "Email settings saved. Blank password fields preserve the existing stored password.";
        return RedirectToAction(nameof(Index), new { section = "email" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(string testEmailRecipient)
    {
        if (string.IsNullOrWhiteSpace(testEmailRecipient))
        {
            TempData["AdminSettingsError"] = "Enter a recipient address for the test email.";
            return RedirectToAction(nameof(Index), new { section = "email" });
        }
        try
        {
            var branding = await _settings.GetBrandingAsync();
            await _email.SendEmailAsync(testEmailRecipient.Trim(), $"{branding.PortalName} Test Email", $"This is a test email from {branding.PortalName}. SMTP configuration is working.");
            await LogAsync("Test", "SystemSettings", $"Sent SMTP test email to {testEmailRecipient.Trim()}.");
            TempData["AdminSettingsMessage"] = $"Test email sent to {testEmailRecipient.Trim()}.";
        }
        catch (Exception ex)
        {
            TempData["AdminSettingsError"] = $"Test email failed: {ex.Message}";
        }
        return RedirectToAction(nameof(Index), new { section = "email" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStorage(StorageSettingsInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminSettingsError"] = "Storage settings were not saved.";
            return RedirectToAction(nameof(Index), new { section = "storage" });
        }
        await _settings.SaveStorageAsync(model);
        await LogAsync("Update", "SystemSettings", "Updated document and worship library storage paths.");
        TempData["AdminSettingsMessage"] = "Storage settings saved and are active immediately.";
        return RedirectToAction(nameof(Index), new { section = "storage" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestPath(string pathType)
    {
        var storage = await _settings.GetStorageAsync();
        var path = string.Equals(pathType, "worship", StringComparison.OrdinalIgnoreCase)
            ? storage.WorshipLibraryRootPath
            : storage.DocumentLibraryRootPath;
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                throw new DirectoryNotFoundException($"Path not found or inaccessible: {path}");
            _ = Directory.EnumerateFileSystemEntries(path).Take(1).ToList();
            TempData["AdminSettingsMessage"] = $"Path test successful: {path}";
        }
        catch (Exception ex)
        {
            TempData["AdminSettingsError"] = $"Path test failed: {ex.Message}";
        }
        return RedirectToAction(nameof(Index), new { section = "storage" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveModules(List<string>? enabledModules)
    {
        await _settings.SaveModulesAsync(enabledModules ?? new List<string>());
        await LogAsync("Update", "SystemSettings", "Updated enabled portal modules.");
        TempData["AdminSettingsMessage"] = "Module configuration saved. Navigation updates immediately.";
        return RedirectToAction(nameof(Index), new { section = "modules" });
    }

    private async Task LogAsync(string action, string entity, string description)
    {
        int? userId = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) userId = id;
        await _data.LogAuditAsync(action, entity, null, description, userId);
    }
}
