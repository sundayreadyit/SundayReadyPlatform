using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Models;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class PeopleController : Controller
{
    private readonly SqlDataService _data;
    public PeopleController(SqlDataService data) => _data = data;

    public async Task<IActionResult> Index()
    {
        return View(await _data.GetPeopleAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new PersonEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PersonEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        await _data.CreatePersonAsync(model);
        await _data.LogAuditAsync("Create", "Person", null, $"Added person: {model.FirstName} {model.LastName}");
        TempData["SuccessMessage"] = "Person added successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var person = await _data.GetPersonForEditAsync(id);
        if (person == null) return NotFound();
        return View(person);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PersonEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        await _data.UpdatePersonAsync(model);
        await _data.LogAuditAsync("Update", "Person", model.PersonId, $"Updated person: {model.FirstName} {model.LastName}");
        TempData["SuccessMessage"] = "Person updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _data.DeactivatePersonAsync(id);
        await _data.LogAuditAsync("Deactivate", "Person", id, $"Deactivated person record ID {id}");
        TempData["SuccessMessage"] = "Person deactivated.";
        return RedirectToAction(nameof(Index));
    }
}
