using ChurchAssetTracker.Data;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly SqlDataService _data;
    private readonly SystemSettingsService _settings;

    public DashboardController(SqlDataService data, SystemSettingsService settings)
    {
        _data = data;
        _settings = settings;
    }

    public async Task<IActionResult> Index(string studentSearch = "", string assetSearch = "")
    {
        ViewBag.StudentSearch = studentSearch;
        ViewBag.AssetSearch = assetSearch;

        var profile = User.IsInRole("Admin") ? "Admin"
            : (User.IsInRole("ITAdmin") || User.IsInRole("ITSupportManager") || User.IsInRole("ITSupportTech")) ? "IT"
            : (User.IsInRole("SchoolAdmin") || User.IsInRole("SchoolStaff")) ? "School"
            : (User.IsInRole("Pastor") || User.IsInRole("ReservationManager") || User.IsInRole("AssetManager") || User.IsInRole("KeyManager")) ? "Church"
            : "Default";
        ViewBag.DashboardWidgetOrder = await _settings.GetDashboardLayoutAsync(profile);
        ViewBag.DashboardProfile = profile;
        var model = await _data.GetEnhancedDashboardAsync(studentSearch, assetSearch);
        return View(model);
    }
}