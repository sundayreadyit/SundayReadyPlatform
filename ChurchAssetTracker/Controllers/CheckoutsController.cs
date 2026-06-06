using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,AssetManager")]
public class CheckoutsController : Controller
{
    private readonly SqlDataService _data;
    public CheckoutsController(SqlDataService data) => _data = data;

    public async Task<IActionResult> Index()
    {
        return View(await _data.GetCheckoutsAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CheckoutCreateViewModel
        {
            AvailableAssets = await _data.GetAvailableAssetOptionsAsync(),
            ActivePeople = await _data.GetActivePeopleOptionsAsync(),
            ExpectedReturnDate = DateTime.Today.AddDays(7),
            QuantityOut = 1
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CheckoutCreateViewModel model)
    {
        model.AvailableAssets = await _data.GetAvailableAssetOptionsAsync();
        model.ActivePeople = await _data.GetActivePeopleOptionsAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var availableQuantity = await _data.GetAvailableAssetQuantityAsync(model.AssetId);
        if (model.QuantityOut > availableQuantity)
        {
            ModelState.AddModelError(nameof(model.QuantityOut), $"Only {availableQuantity} available for this asset.");
            return View(model);
        }

        await _data.CreateCheckoutAsync(model);
        await _data.LogAuditAsync("Checkout", "AssetCheckout", null, $"Checked out quantity {model.QuantityOut} of asset ID {model.AssetId} to person ID {model.PersonId}");
        TempData["SuccessMessage"] = "Asset checked out successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Return(int id)
    {
        var model = await _data.GetCheckoutForReturnAsync(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(CheckoutReturnViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        await _data.ReturnCheckoutAsync(model);
        await _data.LogAuditAsync("Return", "AssetCheckout", model.CheckoutId, $"Returned checkout ID {model.CheckoutId}");
        TempData["SuccessMessage"] = "Asset returned successfully.";
        return RedirectToAction(nameof(Index));
    }
}
