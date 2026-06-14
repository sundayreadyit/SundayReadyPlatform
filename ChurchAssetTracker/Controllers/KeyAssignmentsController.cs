using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,KeyManager")]
public class KeyAssignmentsController : Controller
{
    private readonly SqlDataService _data;
    public KeyAssignmentsController(SqlDataService data) => _data = data;

    public async Task<IActionResult> Index()
    {
        return View(await _data.GetKeyAssignmentsAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Details(int personId)
    {
        var model = await _data.GetKeyAssignmentPersonDetailsAsync(personId);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditPerson(int personId)
    {
        var model = await _data.GetKeyAssignmentPersonEditAsync(personId);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPerson(KeyAssignmentPersonEditViewModel model)
    {
        if (model.PersonId <= 0) return BadRequest();

        await _data.UpdateKeyAssignmentsForPersonAsync(model);
        await _data.LogAuditAsync("Update", "KeyAssignment", model.PersonId, $"Updated key assignments for person ID {model.PersonId}");

        TempData["SuccessMessage"] = "Key assignments updated successfully.";
        return RedirectToAction(nameof(Details), new { personId = model.PersonId });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new KeyAssignmentCreateViewModel
        {
            AvailableKeys = await _data.GetAvailableKeyOptionsAsync(),
            ActivePeople = await _data.GetActivePeopleOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KeyAssignmentCreateViewModel model)
    {
        model.AvailableKeys = await _data.GetAvailableKeyOptionsAsync();
        model.ActivePeople = await _data.GetActivePeopleOptionsAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!await _data.IsKeyAvailableAsync(model.KeyId))
        {
            ModelState.AddModelError(nameof(model.KeyId), "That key is already issued to someone else.");
            return View(model);
        }

        await _data.CreateKeyAssignmentAsync(model);
        await _data.LogAuditAsync("Assign", "KeyAssignment", null, $"Assigned key ID {model.KeyId} to person ID {model.PersonId}");
        TempData["SuccessMessage"] = "Key assigned successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Return(int id)
    {
        var model = await _data.GetKeyAssignmentForReturnAsync(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(KeyAssignmentReturnViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        await _data.ReturnKeyAssignmentAsync(model);
        await _data.LogAuditAsync("Return", "KeyAssignment", model.KeyAssignmentId, $"Returned key assignment ID {model.KeyAssignmentId}");
        TempData["SuccessMessage"] = "Key returned successfully.";
        return RedirectToAction(nameof(Index));
    }
}
