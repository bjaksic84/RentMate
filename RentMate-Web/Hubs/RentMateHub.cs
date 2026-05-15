using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RentMate.Hubs;

/// <summary>
/// SignalR hub for real-time notifications in the RentMate application.
/// Requires authentication for all connections.
/// </summary>
[Authorize]
public class RentMateHub : Hub
{
    #region Constants

    /// <summary>
    /// Client method name for rental request notifications.
    /// </summary>
    public const string RentalRequestedEvent = "RentalRequested";

    /// <summary>
    /// Client method name for rental status change notifications.
    /// </summary>
    public const string RentalStatusChangedEvent = "RentalStatusChanged";

    /// <summary>
    /// Client method name for extension request notifications sent to the owner.
    /// </summary>
    public const string ExtensionRequestedEvent = "ExtensionRequested";

    /// <summary>
    /// Client method name for extension status change notifications sent to the renter.
    /// </summary>
    public const string ExtensionStatusChangedEvent = "ExtensionStatusChanged";

    /// <summary>
    /// Client method name for deposit status change notifications.
    /// </summary>
    public const string DepositStatusChangedEvent = "DepositStatusChanged";

    /// <summary>
    /// Client method name for overdue rental notifications sent to both parties.
    /// </summary>
    public const string RentalOverdueEvent = "RentalOverdue";

    /// <summary>
    /// Client method name for new notification events.
    /// </summary>
    public const string NewNotificationEvent = "NewNotification";

    /// <summary>
    /// Client method name for notification dismissed events.
    /// </summary>
    public const string NotificationDismissedEvent = "NotificationDismissed";

    #endregion

    #region Hub Methods

    /// <summary>
    /// Sends a rental request notification to a specific owner.
    /// </summary>
    /// <param name="ownerId">The user ID of the item owner.</param>
    /// <param name="rentalData">The rental information to send.</param>
    public async Task NotifyRentalRequest(string ownerId, object rentalData)
    {
        await Clients.User(ownerId).SendAsync(RentalRequestedEvent, rentalData);
    }

    /// <summary>
    /// Sends an extension request notification to the item owner.
    /// </summary>
    public async Task NotifyExtensionRequest(string ownerId, object extensionData)
    {
        await Clients.User(ownerId).SendAsync(ExtensionRequestedEvent, extensionData);
    }

    /// <summary>
    /// Sends an extension status change notification to the renter.
    /// </summary>
    public async Task NotifyExtensionStatusChanged(string renterId, object extensionData)
    {
        await Clients.User(renterId).SendAsync(ExtensionStatusChangedEvent, extensionData);
    }

    /// <summary>
    /// Sends a deposit status change notification to the renter.
    /// </summary>
    public async Task NotifyDepositStatusChanged(string renterId, object depositData)
    {
        await Clients.User(renterId).SendAsync(DepositStatusChangedEvent, depositData);
    }

    /// <summary>
    /// Sends an overdue rental notification to a user.
    /// </summary>
    public async Task NotifyRentalOverdue(string userId, object overdueData)
    {
        await Clients.User(userId).SendAsync(RentalOverdueEvent, overdueData);
    }

    #endregion
}

