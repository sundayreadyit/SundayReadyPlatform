using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,KeyManager")]
public class AccessAreasController : Controller
{
    private readonly SqlDataService _data;

    public AccessAreasController(SqlDataService data)
    {
        _data = data;
    }

    public async Task<IActionResult> Index()
    {
        var areas = await _data.GetAccessAreasAsync(includeInactive: true);
        return View(areas);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AccessAreaForm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccessAreaForm model)
    {
        if (string.IsNullOrWhiteSpace(model.AreaName))
            ModelState.AddModelError(nameof(model.AreaName), "Area name is required.");

        if (!ModelState.IsValid) return View(model);

        await _data.CreateAccessAreaAsync(model, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var area = await _data.GetAccessAreaAsync(id);
        if (area == null) return NotFound();

        return View(new AccessAreaForm
        {
            AccessAreaId = area.AccessAreaId,
            AreaName = area.AreaName,
            Description = area.Description,
            IsActive = area.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AccessAreaForm model)
    {
        if (string.IsNullOrWhiteSpace(model.AreaName))
            ModelState.AddModelError(nameof(model.AreaName), "Area name is required.");

        if (!ModelState.IsValid) return View(model);

        await _data.UpdateAccessAreaAsync(model, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _data.SetAccessAreaActiveAsync(id, false, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _data.SetAccessAreaActiveAsync(id, true, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }
}