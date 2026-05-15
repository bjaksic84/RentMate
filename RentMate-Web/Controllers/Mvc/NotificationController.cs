using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentMate.Controllers.Base;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Controllers.Mvc;

/// <summary>
/// Endpoints for the notification bell UI.
/// </summary>
[Authorize]
public class NotificationController(
    UserManager<ApplicationUser> userManager,
    INotificationService notificationService) : BaseAppController(userManager)
{
    /// <summary>
    /// Returns the count of unread, non-dismissed notifications.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var count = await notificationService.GetUnreadCountAsync(userId);
        return Json(new { count });
    }

    /// <summary>
    /// Returns recent notifications grouped by reference for the bell dropdown.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var grouped = await notificationService.GetRecentGroupedAsync(userId);
        var result = grouped.Select(g => new
        {
            g.LatestNotification.Id,
            type = g.LatestNotification.Type.ToString(),
            g.LatestNotification.Title,
            g.LatestNotification.Message,
            g.LatestNotification.ActionUrl,
            g.LatestNotification.IsRead,
            createdAt = g.LatestNotification.CreatedAt.ToString("o"),
            g.Count,
            ids = g.Ids
        });
        return Json(result);
    }

    /// <summary>
    /// Marks a single notification as read.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead([FromBody] NotificationIdRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await notificationService.MarkAsReadAsync(request.Id, userId);
        return Json(new { success = true });
    }

    /// <summary>
    /// Dismisses (hides) a single notification.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss([FromBody] NotificationIdRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await notificationService.DismissAsync(request.Id, userId);
        return Json(new { success = true });
    }

    /// <summary>
    /// Marks all unread notifications as read.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await notificationService.MarkAllAsReadAsync(userId);
        return Json(new { success = true });
    }

    /// <summary>
    /// Request body for single-notification actions.
    /// </summary>
    public record NotificationIdRequest(int Id);
}
