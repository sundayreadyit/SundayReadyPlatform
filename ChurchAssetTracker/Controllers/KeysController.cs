using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,KeyManager")]
public class KeysController : Controller
{
    private readonly SqlDataService _data;

    public KeysController(SqlDataService data) => _data = data;

    public async Task<IActionResult> Index()
    {
        var keys = await _data.GetKeysAsync();
        return View(keys);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new KeyEditViewModel { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KeyEditViewModel key)
    {
        if (!ModelState.IsValid) return View(key);
        await _data.CreateKeyAsync(key);
        TempData["Success"] = "Key added successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var key = await _data.GetKeyForEditAsync(id);
        if (key == null) return NotFound();
        return View(key);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(KeyEditViewModel key)
    {
        if (!ModelState.IsValid) return View(key);
        await _data.UpdateKeyAsync(key);
        TempData["Success"] = "Key updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _data.DeactivateKeyAsync(id);
        TempData["Success"] = "Key deactivated.";
        return RedirectToAction(nameof(Index));
    }
}
