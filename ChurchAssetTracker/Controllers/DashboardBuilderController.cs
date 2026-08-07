using System.Security.Claims;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardBuilderController : Controller
{
    private readonly SystemSettingsService _settings;
    private readonly SqlDataService _data;

    public DashboardBuilderController(SystemSettingsService settings, SqlDataService data)
    {
        _settings = settings;
        _data = data;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string profile = "Default")
    {
        return View(new DashboardBuilderViewModel
        {
            Profile = profile,
            EnabledWidgetKeys = await _settings.GetDashboardLayoutAsync(profile),
            AvailableWidgets = SystemSettingsService.GetDashboardWidgetCatalog().ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string profile, string orderedWidgets = "")
    {
        var keys = orderedWidgets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await _settings.SaveDashboardLayoutAsync(profile, keys);
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        await _data.LogAuditAsync("Update", "DashboardLayout", null, $"Updated {profile} dashboard widget layout.", userId);
        TempData["DashboardBuilderMessage"] = $"{profile} dashboard saved.";
        return RedirectToAction(nameof(Index), new { profile });
    }
}
