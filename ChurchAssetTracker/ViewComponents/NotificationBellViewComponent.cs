using System.Security.Claims;
using ChurchAssetTracker.Data;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAssetTracker.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly SqlDataService _data;

    public NotificationBellViewComponent(SqlDataService data)
    {
        _data = data;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new NotificationCenterViewModel();
        var userIdValue = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
            return View(model);

        model.UnreadCount = await _data.GetUnreadNotificationCountAsync(userId);
        model.RecentNotifications = await _data.GetRecentNotificationsAsync(userId, 8);
        return View(model);
    }
}
