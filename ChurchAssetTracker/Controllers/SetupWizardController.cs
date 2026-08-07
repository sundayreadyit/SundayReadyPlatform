using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class SetupWizardController : Controller
{
    private readonly SystemSettingsService _settings;
    public SetupWizardController(SystemSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Branding = await _settings.GetBrandingAsync();
        ViewBag.Email = await _settings.GetEmailAsync();
        ViewBag.Storage = await _settings.GetStorageAsync();
        ViewBag.Modules = await _settings.GetModulesAsync();
        return View();
    }
}
