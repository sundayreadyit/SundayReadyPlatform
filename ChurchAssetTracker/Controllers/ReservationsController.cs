using ChurchAssetTracker.Data;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace ChurchAssetTracker.Controllers;

[Authorize(Roles = "Admin,ReservationManager,Pastor")]
public class ReservationsController : Controller
{
    private readonly SqlDataService _data;
    private readonly IEmailService _email;

    public ReservationsController(SqlDataService data, IEmailService email)
    {
        _data = data;
        _email = email;
    }

    public async Task<IActionResult> Index(string status = "Active")
    {
        ViewBag.Status = status;
        ViewBag.Summary = await _data.GetReservationDashboardSummaryAsync();
        return View(await _data.GetReservationsAsync(status));
    }

    public async Task<IActionResult> Calendar(int? year, int? month, string visibility = "All", int? accessAreaId = null)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        if (m < 1) { m = 12; y--; }
        if (m > 12) { m = 1; y++; }

        var model = await _data.GetReservationCalendarUpdate1Async(y, m, visibility, accessAreaId);
        return View(model);
    }

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move([FromForm] MoveReservationRequest request)
    {
        if (request.ReservationId <= 0)
            return BadRequest(new MoveReservationResult { Success = false, Message = "Invalid reservation." });

        if (!DateTime.TryParseExact(request.NewDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var newDate))
            return BadRequest(new MoveReservationResult { Success = false, Message = "Invalid date." });

        var result = await _data.MoveReservationToDateAsync(request.ReservationId, newDate, User.Identity?.Name ?? "Unknown");

        if (!result.Success)
            return Conflict(result);

        return Ok(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var reservation = await _data.GetReservationAsync(id);
        return reservation == null ? NotFound() : View(reservation);
    }

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpGet]
    public async Task<IActionResult> Create() => View(await _data.BuildReservationFormAsync());

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReservationForm model)
    {
        await ValidateReservationFields(model);

        if (model.RecurrencePattern != "None" && !model.RecurrenceEndDate.HasValue)
            ModelState.AddModelError(nameof(model.RecurrenceEndDate), "Recurrence end date is required for recurring reservations.");

        if (model.RecurrencePattern != "None" && model.RecurrenceEndDate.HasValue && model.RecurrenceEndDate.Value.Date < model.StartDateTime.Date)
            ModelState.AddModelError(nameof(model.RecurrenceEndDate), "Recurrence end date cannot be before the first reservation date.");

        if (ModelState.IsValid)
        {
            model.Conflicts = await _data.GetRecurringReservationConflictsAsync(model, 0);
            if (model.Conflicts.Any())
                ModelState.AddModelError("", "Conflict blocked: one or more occurrences in this reservation series conflict with an existing pending or approved reservation.");
        }

        if (!ModelState.IsValid)
            return View(await _data.BuildReservationFormAsync(model));

        var result = await _data.CreateReservationOrSeriesAsync(model, User.Identity?.Name ?? "Unknown");
        if (result.Conflicts.Any())
        {
            model.Conflicts = result.Conflicts;
            ModelState.AddModelError("", "Conflict blocked: one or more occurrences in this reservation series conflict with an existing pending or approved reservation.");
            return View(await _data.BuildReservationFormAsync(model));
        }

        TempData["ReservationMessage"] = result.CreatedCount > 1
            ? $"Recurring reservation created with {result.CreatedCount} occurrences."
            : "Reservation created.";

        await TrySendReservationEmailAsync(
            "Reservation Created",
            $"A reservation was created.\n\nEvent: {model.EventName}\nStart: {model.StartDateTime:g}\nEnd: {model.EndDateTime:g}\nStatus: {model.Status}\nCreated by: {User.Identity?.Name}");

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _data.GetReservationFormAsync(id);
        if (model == null) return NotFound();

        model.RecurrencePattern = "None";
        model.RecurrenceEndDate = null;

        return View(model);
    }

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ReservationForm model)
    {
        await ValidateReservationFields(model);

        if (ModelState.IsValid && (model.Status == "Pending" || model.Status == "Approved"))
        {
            model.Conflicts = await _data.GetReservationConflictsAsync(
                model.ReservationId,
                model.AccessAreaId,
                model.StartDateTime,
                model.EndDateTime);

            if (model.Conflicts.Any())
                ModelState.AddModelError("", "Conflict blocked: another pending or approved reservation already uses this room/area during the selected time.");
        }

        if (!ModelState.IsValid)
            return View(await _data.BuildReservationFormAsync(model));

        await _data.UpdateReservationAsync(model, User.Identity?.Name ?? "Unknown");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var reservation = await _data.GetReservationAsync(id);
        if (reservation != null)
        {
            var conflicts = await _data.GetReservationConflictsAsync(id, reservation.AccessAreaId, reservation.StartDateTime, reservation.EndDateTime);
            if (conflicts.Any())
            {
                TempData["ReservationError"] = "Reservation cannot be approved because it conflicts with another pending or approved reservation.";
                return RedirectToAction(nameof(Index));
            }
        }

        await _data.SetReservationStatusAsync(id, "Approved", User.Identity?.Name ?? "Unknown");

        if (reservation != null)
        {
            await TrySendReservationEmailAsync(
                "Reservation Approved",
                $"A reservation was approved.\n\nEvent: {reservation.EventName}\nStart: {reservation.StartDateTime:g}\nEnd: {reservation.EndDateTime:g}\nApproved by: {User.Identity?.Name}");
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deny(int id)
    {
        var reservation = await _data.GetReservationAsync(id);
        await _data.SetReservationStatusAsync(id, "Denied", User.Identity?.Name ?? "Unknown");

        if (reservation != null)
        {
            await TrySendReservationEmailAsync(
                "Reservation Denied",
                $"A reservation was denied.\n\nEvent: {reservation.EventName}\nStart: {reservation.StartDateTime:g}\nDenied by: {User.Identity?.Name}");
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,ReservationManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var reservation = await _data.GetReservationAsync(id);
        await _data.SetReservationStatusAsync(id, "Cancelled", User.Identity?.Name ?? "Unknown");

        if (reservation != null)
        {
            await TrySendReservationEmailAsync(
                "Reservation Cancelled",
                $"A reservation was cancelled.\n\nEvent: {reservation.EventName}\nStart: {reservation.StartDateTime:g}\nCancelled by: {User.Identity?.Name}");
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task TrySendReservationEmailAsync(string subject, string body)
    {
        try
        {
            await _email.SendReservationsEmailAsync($"[CWC Portal] {subject}", body);
        }
        catch
        {
            // Email should never block core reservation workflows.
        }
    }

    private Task ValidateReservationFields(ReservationForm model)
    {
        if (string.IsNullOrWhiteSpace(model.EventName))
            ModelState.AddModelError(nameof(model.EventName), "Event name is required.");

        if (model.EndDateTime <= model.StartDateTime)
            ModelState.AddModelError(nameof(model.EndDateTime), "End date/time must be after start date/time.");

        if (model.AccessAreaId == null)
            ModelState.AddModelError(nameof(model.AccessAreaId), "Room/area is required.");

        if (string.IsNullOrWhiteSpace(model.RecurrencePattern))
            model.RecurrencePattern = "None";

        return Task.CompletedTask;
    }
}