using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,KeyManager")]
public class KeyAccessController : Controller
{
    private readonly SqlDataService _data;

    public KeyAccessController(SqlDataService data)
    {
        _data = data;
    }

    [HttpGet]
    public async Task<IActionResult> Manage(int keyId)
    {
        var model = await _data.GetKeyAccessManageAsync(keyId);
        if (model == null) return NotFound();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Manage(KeyAccessManageViewModel model)
    {
        await _data.UpdateKeyAccessAreasAsync(
            model.KeyId,
            model.SelectedAccessAreaIds ?? new List<int>(),
            User.Identity?.Name ?? "Unknown");

        return RedirectToAction("Index", "Keys");
    }
}