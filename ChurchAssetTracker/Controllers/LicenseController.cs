using ChurchAssetTracker.Models;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class LicenseController : Controller
{
    private readonly LicenseService _licenses;
    public LicenseController(LicenseService licenses) => _licenses = licenses;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new LicenseAdministrationViewModel
        {
            State = await _licenses.GetStateAsync(),
            ProductCode = _licenses.ProductCode,
            InstalledVersion = PortalVersion.Version,
            ApiBaseUrl = _licenses.ApiBaseUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(LicenseAdministrationViewModel model)
    {
        var state = await _licenses.ActivateAsync(model.LicenseKey);
        TempData[state.IsUsable ? "LicenseSuccess" : "LicenseError"] = state.Message ?? state.Status;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revalidate()
    {
        var state = await _licenses.RevalidateAsync();
        TempData[state.IsUsable ? "LicenseSuccess" : "LicenseError"] = state.Message ?? state.Status;
        return RedirectToAction(nameof(Index));
    }
}
