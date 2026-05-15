using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RentMate.Hubs;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.Dto;
using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations;

/// <summary>
/// Persistent notification service with SignalR push and auto-dismiss.
/// </summary>
public class NotificationService(
    RentMateContext context,
    IHubContext<RentMateHub> hubContext) : INotificationService
{
    public async Task CreateAsync(string userId, NotificationType type, string title,
        string? message = null, int? referenceId = null,
        string? referenceType = null, string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            ActionUrl = actionUrl
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        // Push to connected client in real-time
        await hubContext.Clients.User(userId).SendAsync(RentMateHub.NewNotificationEvent, new
        {
            id = notification.Id,
            type = type.ToString(),
            title,
            message,
            actionUrl,
            createdAt = notification.CreatedAt.ToString("o")
        });
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDismissed);
    }

    public async Task<List<Notification>> GetRecentAsync(string userId, int limit = 20)
    {
        return await context.Notifications
            .Where(n => n.UserId == userId && !n.IsDismissed)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<GroupedNotification>> GetRecentGroupedAsync(string userId, int limit = 20)
    {
        var notifications = await context.Notifications
            .Where(n => n.UserId == userId && !n.IsDismissed)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100) // Fetch more for grouping
            .AsNoTracking()
            .ToListAsync();

        return notifications
            .GroupBy(n => n.ReferenceId.HasValue
                ? new { ReferenceId = (int?)n.ReferenceId, n.ReferenceType, n.Type }
                : new { ReferenceId = (int?)n.Id, ReferenceType = (string?)null, n.Type }) // Don't group null-reference notifications
            .Select(g => new GroupedNotification
            {
                LatestNotification = g.First(),
                Count = g.Count(),
                Ids = g.Select(n => n.Id).ToList()
            })
            .Take(limit)
            .ToList();
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task DismissAsync(int notificationId, string userId)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null && !notification.IsDismissed)
        {
            notification.IsDismissed = true;
            notification.IsRead = true;
            notification.ReadAt ??= DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsDismissed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public async Task AutoDismissAsync(int referenceId, string referenceType, NotificationType? specificType = null)
    {
        var query = context.Notifications
            .Where(n => n.ReferenceId == referenceId
                && n.ReferenceType == referenceType
                && !n.IsDismissed);

        if (specificType.HasValue)
            query = query.Where(n => n.Type == specificType.Value);

        var notifications = await query.ToListAsync();
        if (notifications.Count == 0) return;

        // Group by user to send a single batched SignalR event per user
        var byUser = new Dictionary<string, List<int>>();
        foreach (var n in notifications)
        {
            n.IsDismissed = true;
            n.IsRead = true;
            n.ReadAt ??= DateTime.UtcNow;

            if (!byUser.TryGetValue(n.UserId, out var ids))
            {
                ids = [];
                byUser[n.UserId] = ids;
            }
            ids.Add(n.Id);
        }

        await context.SaveChangesAsync();

        // Push batched dismiss events per user
        foreach (var (userId, ids) in byUser)
        {
            await hubContext.Clients.User(userId).SendAsync(
                RentMateHub.NotificationDismissedEvent,
                new { ids });
        }
    }
}
