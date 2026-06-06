using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly SqlDataService _data;

    public DashboardController(SqlDataService data)
    {
        _data = data;
    }

    public async Task<IActionResult> Index(string studentSearch = "", string assetSearch = "")
    {
        ViewBag.StudentSearch = studentSearch;
        ViewBag.AssetSearch = assetSearch;

        var model = await _data.GetEnhancedDashboardAsync(studentSearch, assetSearch);
        return View(model);
    }
}