using System.Security.Claims;
using ChurchAssetTracker.Data;
using ChurchAssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class ITSupportController : Controller
{
    private readonly SqlDataService _data;
    private readonly IEmailService _email;

    public ITSupportController(SqlDataService data, IEmailService email)
    {
        _data = data;
        _email = email;
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    public async Task<IActionResult> Index(string status = "Open")
    {
        ViewBag.Status = status;
        ViewBag.Summary = await _data.GetITSupportDashboardSummaryAsync();
        var tickets = await _data.GetITSupportTicketsAsync(status);
        return View(tickets);
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    public async Task<IActionResult> TechnicianDashboard()
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        var model = await _data.GetITSupportTechnicianDashboardAsync(userId);
        return View(model);
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _data.GetITSupportTicketAsync(id);
        if (ticket == null) return NotFound();

        var comments = await _data.GetITSupportTicketCommentsAsync(id);

        return View(new ITSupportTicketDetailsViewModel
        {
            Ticket = ticket,
            Comments = comments,
            NewComment = new ITSupportCommentForm { TicketId = id }
        });
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = await _data.BuildITSupportTicketFormAsync();
        model.RequestedByUserId = CurrentUserId > 0 ? CurrentUserId : null;
        await _data.ApplyRequesterUserContactAsync(model);
        await ApplyAssignableUserFilterAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ITSupportTicketForm model)
    {
        await ApplyRequesterContactWithoutLosingManualPhoneAsync(model);

        ValidateTicket(model);

        if (!ModelState.IsValid)
        {
            var rebuilt = await _data.BuildITSupportTicketFormAsync(model);
            await ApplyAssignableUserFilterAsync(rebuilt);
            return View(rebuilt);
        }

        var id = await _data.CreateITSupportTicketAsync(model, User.Identity?.Name ?? "Unknown");

        await SendCreateEmailAsync(id, model);
        await NotifyTicketCreatedByITAsync(id, model);

        return RedirectToAction(nameof(Details), new { id });
    }


    private async Task ApplyRequesterContactWithoutLosingManualPhoneAsync(ITSupportTicketForm model)
    {
        if (!model.RequestedByUserId.HasValue) return;

        var enteredName = model.RequestedByName;
        var enteredEmail = model.RequestedByEmail;
        var enteredPhone = model.RequestedByPhone;

        await _data.ApplyRequesterUserContactAsync(model);

        // Keep manually entered values when the selected Portal User profile does not have them populated.
        if (string.IsNullOrWhiteSpace(model.RequestedByName) && !string.IsNullOrWhiteSpace(enteredName))
            model.RequestedByName = enteredName;

        if (string.IsNullOrWhiteSpace(model.RequestedByEmail) && !string.IsNullOrWhiteSpace(enteredEmail))
            model.RequestedByEmail = enteredEmail;

        if (string.IsNullOrWhiteSpace(model.RequestedByPhone) && !string.IsNullOrWhiteSpace(enteredPhone))
            model.RequestedByPhone = enteredPhone;
    }

    [Authorize(Roles = "ITRequester")]
    public async Task<IActionResult> MyTickets()
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        var tickets = await _data.GetITSupportTicketsForRequesterAsync(userId);
        return View(tickets);
    }

    [Authorize(Roles = "ITRequester")]
    [HttpGet]
    public async Task<IActionResult> RequesterCreate()
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        var model = await _data.BuildRequesterITSupportTicketFormAsync(userId);
        return View(model);
    }

    [Authorize(Roles = "ITRequester")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequesterCreate(ITSupportTicketForm model)
    {
        var requesterUserId = CurrentUserId;
        if (requesterUserId <= 0) return Unauthorized();

        model.RequestedByUserId = requesterUserId;
        await _data.ApplyRequesterUserContactAsync(model);

        ValidateRequesterTicket(model);

        if (!ModelState.IsValid)
        {
            var rebuilt = await _data.BuildRequesterITSupportTicketFormAsync(CurrentUserId);
            rebuilt.Title = model.Title;
            rebuilt.Description = model.Description;
            rebuilt.Category = model.Category;
            rebuilt.Priority = model.Priority;
            rebuilt.RequestedByName = model.RequestedByName;
            rebuilt.RequestedByEmail = model.RequestedByEmail;
            rebuilt.RequestedByPhone = model.RequestedByPhone;
            rebuilt.RequestedByPersonId = model.RequestedByPersonId;
            rebuilt.RequestedByUserId = model.RequestedByUserId;
            rebuilt.ITAssetId = model.ITAssetId;
            rebuilt.AccessAreaId = model.AccessAreaId;
            return View(rebuilt);
        }

        model.Status = "New";
        model.AssignedToUserId = null;
        model.DueDate = null;

        var id = await _data.CreateITSupportTicketAsync(model, User.Identity?.Name ?? "Unknown");

        await TrySendITSupportEmailAsync(
            null,
            "IT Ticket Submitted",
            $@"A new IT support ticket was submitted.

Ticket: IT-{id.ToString("00000")}
Title: {model.Title}
Priority: {model.Priority}
Requester: {model.RequestedByName}
Submitted by: {User.Identity?.Name}

Description:
{model.Description}");

        await NotifyRequesterTicketSubmittedAsync(id, model);

        return RedirectToAction(nameof(RequesterDetails), new { id });
    }

    [Authorize(Roles = "ITRequester")]
    public async Task<IActionResult> RequesterDetails(int id)
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        if (!await _data.CanUserViewRequesterTicketAsync(id, userId))
            return Forbid();

        var ticket = await _data.GetITSupportTicketAsync(id);
        if (ticket == null) return NotFound();

        var comments = await _data.GetITSupportTicketCommentsAsync(id);

        return View(new ITSupportTicketDetailsViewModel
        {
            Ticket = ticket,
            Comments = comments,
            NewComment = new ITSupportCommentForm { TicketId = id }
        });
    }

    [Authorize(Roles = "ITRequester")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequesterAddComment(int ticketId, string commentText)
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        if (!await _data.CanUserViewRequesterTicketAsync(ticketId, userId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(commentText))
        {
            TempData["TicketError"] = "Comment cannot be blank.";
            return RedirectToAction(nameof(RequesterDetails), new { id = ticketId });
        }

        await _data.AddITSupportTicketCommentAsync(new ITSupportCommentForm
        {
            TicketId = ticketId,
            CommentText = commentText,
            IsInternal = false
        }, CurrentUserId, CurrentUserDisplayName);

        var assignedEmail = await _data.GetITSupportTicketAssignedUserEmailAsync(ticketId);

        await TrySendITSupportEmailAsync(
            assignedEmail,
            "IT Ticket Requester Comment Added",
            $@"A requester added a comment to IT ticket #{ticketId}.

Added by: {CurrentUserDisplayName}

Comment:
{commentText}");

        await NotifyRequesterCommentAddedAsync(ticketId, commentText);

        return RedirectToAction(nameof(RequesterDetails), new { id = ticketId });
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _data.GetITSupportTicketFormAsync(id);
        if (model == null) return NotFound();

        await ApplyAssignableUserFilterAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ITSupportTicketForm model)
    {
        await ApplyRequesterContactWithoutLosingManualPhoneAsync(model);

        ValidateTicket(model);

        if (!ModelState.IsValid)
        {
            var rebuilt = await _data.BuildITSupportTicketFormAsync(model);
            await ApplyAssignableUserFilterAsync(rebuilt);
            return View(rebuilt);
        }

        await _data.UpdateITSupportTicketAsync(model, User.Identity?.Name ?? "Unknown");

        var assignedEmail = await _data.GetUserEmailByUserIdAsync(model.AssignedToUserId);

        await TrySendITSupportEmailAsync(
            assignedEmail,
            "IT Ticket Updated",
            $@"An IT support ticket was updated.

Ticket: IT-{model.TicketId.ToString("00000")}
Title: {model.Title}
Priority: {model.Priority}
Status: {model.Status}
Updated by: {User.Identity?.Name}

Description:
{model.Description}");

        await NotifyTicketUpdatedAsync(model);

        return RedirectToAction(nameof(Details), new { id = model.TicketId });
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech,ITRequester")]
    [HttpGet]
    public async Task<IActionResult> PersonContact(int id)
    {
        var contact = await _data.GetPersonContactInfoAsync(id);
        if (contact == null) return NotFound();

        return Json(new
        {
            fullName = contact.FullName,
            email = contact.Email,
            phone = contact.Phone
        });
    }



    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpGet]
    public async Task<IActionResult> UserContact(int id)
    {
        var user = await _data.GetITSupportRequesterUserOptionAsync(id);
        if (user == null) return NotFound();

        return Json(new
        {
            fullName = user.DisplayName,
            email = user.Email,
            phone = user.Phone
        });
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int ticketId, string commentText, bool isInternal = false)
    {
        if (ticketId <= 0) return NotFound();

        if (string.IsNullOrWhiteSpace(commentText))
        {
            TempData["TicketError"] = "Comment cannot be blank.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        await _data.AddITSupportTicketCommentAsync(new ITSupportCommentForm
        {
            TicketId = ticketId,
            CommentText = commentText,
            IsInternal = isInternal
        }, CurrentUserId, CurrentUserDisplayName);

        var assignedEmail = await _data.GetITSupportTicketAssignedUserEmailAsync(ticketId);

        await TrySendITSupportEmailAsync(
            assignedEmail,
            isInternal ? "IT Ticket Internal Note Added" : "IT Ticket Comment Added",
            $@"A comment was added to IT ticket #{ticketId}.

Added by: {CurrentUserDisplayName}

Comment:
{commentText}");

        await NotifyITCommentAddedAsync(ticketId, commentText, isInternal);

        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAssigned(int id) { await ChangeStatusAndNotifyAsync(id, "Assigned"); return RedirectToAction(nameof(Details), new { id }); }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkInProgress(int id) { await ChangeStatusAndNotifyAsync(id, "In Progress"); return RedirectToAction(nameof(Details), new { id }); }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WaitingOnUser(int id) { await ChangeStatusAndNotifyAsync(id, "Waiting on User"); return RedirectToAction(nameof(Details), new { id }); }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id) { await ChangeStatusAndNotifyAsync(id, "Resolved"); return RedirectToAction(nameof(Details), new { id }); }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id) { await ChangeStatusAndNotifyAsync(id, "Closed"); return RedirectToAction(nameof(Details), new { id }); }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id) { await ChangeStatusAndNotifyAsync(id, "Cancelled"); return RedirectToAction(nameof(Details), new { id }); }

    [Authorize(Roles = "Admin,ITAdmin,ITSupportManager,ITSupportTech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id) { await ChangeStatusAndNotifyAsync(id, "In Progress"); return RedirectToAction(nameof(Details), new { id }); }

    private int CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }
    }



    private string CurrentUserDisplayName
    {
        get
        {
            var displayName = User.FindFirstValue("DisplayName");
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            var name = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            var username = User.FindFirstValue(ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(username))
                return username;

            return "Unknown";
        }
    }

    private string TechnicianTicketLink(int ticketId) => Url.Action("Details", "ITSupport", new { id = ticketId }) ?? $"/ITSupport/Details/{ticketId}";
    private string RequesterTicketLink(int ticketId) => Url.Action("RequesterDetails", "ITSupport", new { id = ticketId }) ?? $"/ITSupport/RequesterDetails/{ticketId}";

    private async Task ApplyAssignableUserFilterAsync(ITSupportTicketForm model)
    {
        var assignableIds = await _data.GetITAssignableUserIdsAsync();

        model.Users = model.Users
            .Where(u => assignableIds.Contains(u.UserId))
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    private async Task NotifyRequesterTicketSubmittedAsync(int ticketId, ITSupportTicketForm model)
    {
        await _data.NotifyITSupportManagersAsync(
            "New IT ticket submitted",
            $"{model.RequestedByName} submitted ticket IT-{ticketId.ToString("00000")}: {model.Title}",
            "ITSupport",
            ticketId,
            TechnicianTicketLink(ticketId),
            CurrentUserId);
    }

    private async Task NotifyTicketCreatedByITAsync(int ticketId, ITSupportTicketForm model)
    {
        await _data.NotifyITSupportAssignedUserAsync(
            model.AssignedToUserId,
            "IT ticket assigned to you",
            $"Ticket IT-{ticketId.ToString("00000")} was assigned to you: {model.Title}",
            "ITSupport",
            ticketId,
            TechnicianTicketLink(ticketId),
            CurrentUserId);

        await _data.NotifyITSupportRequesterAsync(
            ticketId,
            "IT ticket created",
            $"An IT ticket was created for you: {model.Title}",
            "ITSupport",
            RequesterTicketLink(ticketId),
            CurrentUserId);
    }

    private async Task NotifyTicketUpdatedAsync(ITSupportTicketForm model)
    {
        await _data.NotifyITSupportAssignedUserAsync(
            model.AssignedToUserId,
            "IT ticket updated",
            $"Ticket IT-{model.TicketId.ToString("00000")} was updated: {model.Title}",
            "ITSupport",
            model.TicketId,
            TechnicianTicketLink(model.TicketId),
            CurrentUserId);

        await _data.NotifyITSupportRequesterAsync(
            model.TicketId,
            "IT ticket updated",
            $"Your IT ticket was updated: {model.Title}",
            "ITSupport",
            RequesterTicketLink(model.TicketId),
            CurrentUserId);
    }

    private async Task NotifyRequesterCommentAddedAsync(int ticketId, string commentText)
    {
        var ticket = await _data.GetITSupportTicketAsync(ticketId);
        if (ticket == null) return;

        await _data.NotifyITSupportAssignedUserAsync(
            ticket.AssignedToUserId,
            "Requester replied to IT ticket",
            $"{User.Identity?.Name} replied to {ticket.TicketNumber}: {Shorten(commentText)}",
            "ITSupport",
            ticketId,
            TechnicianTicketLink(ticketId),
            CurrentUserId);

        if (!ticket.AssignedToUserId.HasValue)
        {
            await _data.NotifyITSupportManagersAsync(
                "Requester replied to unassigned IT ticket",
                $"{User.Identity?.Name} replied to {ticket.TicketNumber}: {Shorten(commentText)}",
                "ITSupport",
                ticketId,
                TechnicianTicketLink(ticketId),
                CurrentUserId);
        }
    }

    private async Task NotifyITCommentAddedAsync(int ticketId, string commentText, bool isInternal)
    {
        var ticket = await _data.GetITSupportTicketAsync(ticketId);
        if (ticket == null) return;

        if (isInternal)
        {
            await _data.NotifyITSupportAssignedUserAsync(
                ticket.AssignedToUserId,
                "Internal note added",
                $"An internal note was added to {ticket.TicketNumber}: {Shorten(commentText)}",
                "ITSupport",
                ticketId,
                TechnicianTicketLink(ticketId),
                CurrentUserId);
            return;
        }

        await _data.NotifyITSupportRequesterAsync(
            ticketId,
            "IT replied to your ticket",
            $"IT replied to {ticket.TicketNumber}: {Shorten(commentText)}",
            "ITSupport",
            RequesterTicketLink(ticketId),
            CurrentUserId);
    }

    private async Task SendCreateEmailAsync(int id, ITSupportTicketForm model)
    {
        var assignedEmail = await _data.GetUserEmailByUserIdAsync(model.AssignedToUserId);

        await TrySendITSupportEmailAsync(
            assignedEmail,
            "IT Ticket Created",
            $@"A new IT support ticket was created.

Ticket: IT-{id.ToString("00000")}
Title: {model.Title}
Priority: {model.Priority}
Status: {model.Status}
Requester: {model.RequestedByName}
Created by: {User.Identity?.Name}

Description:
{model.Description}");
    }

    private async Task ChangeStatusAndNotifyAsync(int ticketId, string newStatus)
    {
        var ticket = await _data.GetITSupportTicketAsync(ticketId);
        if (ticket == null) return;

        await _data.UpdateITSupportTicketStatusAsync(ticketId, newStatus, User.Identity?.Name ?? "Unknown");

        var assignedEmail = await _data.GetITSupportTicketAssignedUserEmailAsync(ticketId);

        await TrySendITSupportEmailAsync(
            assignedEmail,
            $"IT Ticket {newStatus}",
            $@"An IT support ticket status was changed.

Ticket: {ticket.TicketNumber}
Title: {ticket.Title}
New Status: {newStatus}
Changed by: {User.Identity?.Name}");

        await _data.NotifyITSupportAssignedUserAsync(
            ticket.AssignedToUserId,
            $"IT ticket {newStatus}",
            $"{ticket.TicketNumber} status changed to {newStatus}: {ticket.Title}",
            "ITSupport",
            ticketId,
            TechnicianTicketLink(ticketId),
            CurrentUserId);

        await _data.NotifyITSupportRequesterAsync(
            ticketId,
            $"IT ticket {newStatus}",
            $"Your IT ticket {ticket.TicketNumber} status changed to {newStatus}: {ticket.Title}",
            "ITSupport",
            RequesterTicketLink(ticketId),
            CurrentUserId);
    }

    private async Task TrySendITSupportEmailAsync(string? assignedUserEmail, string subject, string body)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(assignedUserEmail))
            {
                await _email.SendEmailAsync(assignedUserEmail, $"[CWC Portal] {subject}", body);
                return;
            }

            await _email.SendITSupportEmailAsync($"[CWC Portal] {subject}", body);
        }
        catch
        {
            // Email should never block ticket workflows.
        }
    }

    private void ValidateTicket(ITSupportTicketForm model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");

        if (string.IsNullOrWhiteSpace(model.Priority))
            ModelState.AddModelError(nameof(model.Priority), "Priority is required.");

        if (string.IsNullOrWhiteSpace(model.Status))
            ModelState.AddModelError(nameof(model.Status), "Status is required.");
    }

    private void ValidateRequesterTicket(ITSupportTicketForm model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");

        if (string.IsNullOrWhiteSpace(model.Description))
            ModelState.AddModelError(nameof(model.Description), "Description is required.");

        if (string.IsNullOrWhiteSpace(model.RequestedByName))
            ModelState.AddModelError(nameof(model.RequestedByName), "Requester name is required.");

        if (string.IsNullOrWhiteSpace(model.Priority))
            ModelState.AddModelError(nameof(model.Priority), "Priority is required.");
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();

        return value.Length <= 120
            ? value
            : value.Substring(0, 120) + "...";
    }
}