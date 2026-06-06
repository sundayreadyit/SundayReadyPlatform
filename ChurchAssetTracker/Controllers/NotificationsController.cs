using System.Security.Claims;
using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly SqlDataService _data;

    public NotificationsController(SqlDataService data)
    {
        _data = data;
    }

    public async Task<IActionResult> Index(bool unreadOnly = false)
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        ViewBag.UnreadOnly = unreadOnly;
        return View(await _data.GetNotificationsAsync(userId, unreadOnly, 200));
    }

    public async Task<IActionResult> Open(int id)
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        var notification = await _data.GetNotificationAsync(id, userId);
        if (notification == null) return NotFound();

        await _data.MarkNotificationReadAsync(id, userId);

        if (!string.IsNullOrWhiteSpace(notification.LinkUrl) && Url.IsLocalUrl(notification.LinkUrl))
            return LocalRedirect(notification.LinkUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        await _data.MarkNotificationReadAsync(id, userId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = CurrentUserId;
        if (userId <= 0) return Unauthorized();

        await _data.MarkAllNotificationsReadAsync(userId);
        return RedirectToAction(nameof(Index));
    }

    private int CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }
    }
}
