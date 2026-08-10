using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class SetupWizardController : Controller
{
    private readonly SystemSettingsService _settings;
    private readonly LicenseService _licenses;
    public SetupWizardController(SystemSettingsService settings, LicenseService licenses) { _settings = settings; _licenses = licenses; }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Branding = await _settings.GetBrandingAsync();
        ViewBag.Email = await _settings.GetEmailAsync();
        ViewBag.Storage = await _settings.GetStorageAsync();
        ViewBag.Modules = await _settings.GetModulesAsync();
        ViewBag.License = await _licenses.GetStateAsync();
        return View();
    }
}
