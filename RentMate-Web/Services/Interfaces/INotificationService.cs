using RentMate.Models.Domain;
using RentMate.Models.Dto;

namespace RentMate.Services.Interfaces;

/// <summary>
/// Manages persistent notification lifecycle: creation, retrieval, dismissal, and auto-resolution.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a notification, saves to DB, and pushes via SignalR.
    /// </summary>
    Task CreateAsync(string userId, NotificationType type, string title,
        string? message = null, int? referenceId = null,
        string? referenceType = null, string? actionUrl = null);

    /// <summary>
    /// Returns count of unread, non-dismissed notifications for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>
    /// Returns recent non-dismissed notifications for a user, newest first.
    /// </summary>
    Task<List<Notification>> GetRecentAsync(string userId, int limit = 20);

    /// <summary>
    /// Returns recent notifications grouped by (ReferenceId, ReferenceType, Type) to collapse duplicates.
    /// </summary>
    Task<List<GroupedNotification>> GetRecentGroupedAsync(string userId, int limit = 20);

    /// <summary>
    /// Marks a single notification as read.
    /// </summary>
    Task MarkAsReadAsync(int notificationId, string userId);

    /// <summary>
    /// Dismisses (hides) a single notification.
    /// </summary>
    Task DismissAsync(int notificationId, string userId);

    /// <summary>
    /// Marks all unread notifications as read for a user.
    /// </summary>
    Task MarkAllAsReadAsync(string userId);

    /// <summary>
    /// Auto-dismisses notifications matching the given reference when the issue resolves.
    /// </summary>
    Task AutoDismissAsync(int referenceId, string referenceType, NotificationType? specificType = null);
}
