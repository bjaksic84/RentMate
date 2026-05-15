using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using RentMate.Hubs;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentMate.Shared.Contracts.Responses;
using NotificationType = RentMate.Models.Domain.NotificationType;

namespace RentMate.Services.Implementations;

/// <inheritdoc cref="INotificationDispatcher"/>
public class NotificationDispatcher(
    IHubContext<RentMateHub> hub,
    INotificationService notifications,
    IStringLocalizer<NotificationDispatcher> localizer) : INotificationDispatcher
{
    // ── Rental lifecycle events ───────────────────────────────────

    public async Task RentalRequestedAsync(int rentalId, string ownerId, string? itemTitle,
        string? renterEmail, string? renterName, DateTime startDate, DateTime endDate)
    {
        await hub.Clients.User(ownerId).SendAsync(RentMateHub.RentalRequestedEvent, new
        {
            rentalId,
            itemTitle,
            renterEmail,
            startDate = startDate.ToShortDateString(),
            endDate = endDate.ToShortDateString(),
            status = "Pending"
        });

        await notifications.CreateAsync(
            ownerId, NotificationType.RentalRequested,
            localizer["Notification.RentalRequested"].Value,
            string.Format(localizer["NotificationMsg.RentalRequested"].Value, renterName, itemTitle),
            rentalId, "Rental", "/Dashboard?tab=lending");
    }

    public async Task RentalStatusChangedAsync(int rentalId, string renterId, string? itemTitle,
        RentalStatus status, string? message = null)
    {
        await hub.Clients.User(renterId).SendAsync(RentMateHub.RentalStatusChangedEvent, new
        {
            rentalId,
            newStatus = status.ToString(),
            itemTitle,
            message
        });

        var notifType = status switch
        {
            RentalStatus.Accepted => NotificationType.RentalAccepted,
            RentalStatus.Completed => NotificationType.RentalCompleted,
            RentalStatus.Cancelled => NotificationType.RentalCancelled,
            _ => NotificationType.RentalApproved
        };

        await notifications.CreateAsync(
            renterId, notifType,
            localizer["Notification." + notifType].Value,
            string.Format(localizer["NotificationMsg." + notifType].Value, itemTitle ?? ""),
            rentalId, "Rental", "/Dashboard?tab=renting");

        if (status != RentalStatus.Pending)
            await notifications.AutoDismissAsync(rentalId, "Rental", NotificationType.RentalRequested);
    }

    // ── Deposit events ────────────────────────────────────────────

    public async Task DepositReleasedAsync(int rentalId, string renterId, string? itemTitle)
    {
        await hub.Clients.User(renterId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status = "Released", itemTitle });

        await notifications.CreateAsync(
            renterId, NotificationType.DepositReleased,
            localizer["Notification.DepositReleased"].Value,
            string.Format(localizer["NotificationMsg.DepositReleased"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");

        await notifications.AutoDismissAsync(rentalId, "Deposit");
    }

    public async Task DepositChargedAsync(int rentalId, string renterId, string? itemTitle, decimal? amount = null, string? reason = null)
    {
        await hub.Clients.User(renterId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status = "Charged", itemTitle, amount, reason });

        await notifications.CreateAsync(
            renterId, NotificationType.DepositCharged,
            localizer["Notification.DepositCharged"].Value,
            string.Format(localizer["NotificationMsg.DepositCharged"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");
    }

    public async Task DepositDisputedAsync(int rentalId, string ownerId, string? itemTitle)
    {
        await hub.Clients.User(ownerId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status = "Disputed", itemTitle });

        await notifications.CreateAsync(
            ownerId, NotificationType.DepositDisputed,
            localizer["Notification.DepositDisputed"].Value,
            string.Format(localizer["NotificationMsg.DepositDisputed"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");

        await notifications.AutoDismissAsync(rentalId, "Deposit", NotificationType.DepositCharged);
    }

    public async Task DepositCounterOfferedAsync(int rentalId, string renterId, string? itemTitle)
    {
        await hub.Clients.User(renterId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status = "CounterOffered", itemTitle });

        await notifications.CreateAsync(
            renterId, NotificationType.DepositCounterOffered,
            localizer["Notification.DepositCounterOffered"].Value,
            string.Format(localizer["NotificationMsg.DepositCounterOffered"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");

        await notifications.AutoDismissAsync(rentalId, "Deposit", NotificationType.DepositDisputed);
    }

    public async Task DepositResolvedAsync(int rentalId, string recipientId, string? itemTitle, string status = "ChargeAccepted")
    {
        await hub.Clients.User(recipientId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status, itemTitle });

        await notifications.CreateAsync(
            recipientId, NotificationType.DepositResolved,
            localizer["Notification.DepositResolved"].Value,
            string.Format(localizer["NotificationMsg.DepositResolved"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");

        await notifications.AutoDismissAsync(rentalId, "Deposit");
    }

    public async Task DepositEscalatedAsync(int rentalId, string recipientId, string? itemTitle)
    {
        await hub.Clients.User(recipientId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status = "Escalated", itemTitle });

        await notifications.CreateAsync(
            recipientId, NotificationType.DepositEscalated,
            localizer["Notification.DepositEscalated"].Value,
            string.Format(localizer["NotificationMsg.DepositEscalated"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");
    }

    public async Task DepositCounterRejectedAsync(int rentalId, string ownerId, string? itemTitle, bool escalated)
    {
        await hub.Clients.User(ownerId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status = "CounterRejected", itemTitle });

        var type = escalated ? NotificationType.DepositEscalated : NotificationType.DepositDisputed;

        await notifications.CreateAsync(
            ownerId, type,
            localizer["Notification.DepositCounterRejected"].Value,
            string.Format(localizer["NotificationMsg.DepositCounterRejected"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");
    }

    public async Task DepositAdminResolvedAsync(int rentalId, string ownerId, string renterId, string? itemTitle, decimal amount, string? adminNotes = null)
    {
        var status = amount == 0 ? "Released" : "ChargeUpheld";
        await hub.Clients.User(ownerId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status, itemTitle, adminNotes });
        await hub.Clients.User(renterId).SendAsync(RentMateHub.DepositStatusChangedEvent,
            new { rentalId, status, itemTitle, adminNotes });

        await notifications.CreateAsync(
            ownerId, NotificationType.DepositResolved,
            localizer["Notification.DepositResolved"].Value,
            string.Format(localizer["NotificationMsg.DepositResolved"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");

        await notifications.CreateAsync(
            renterId, NotificationType.DepositResolved,
            localizer["Notification.DepositResolved"].Value,
            string.Format(localizer["NotificationMsg.DepositResolved"].Value, itemTitle),
            rentalId, "Deposit", "/Dashboard");

        await notifications.AutoDismissAsync(rentalId, "Deposit");
    }

    // ── Extension events ──────────────────────────────────────────

    public async Task ExtensionRequestedAsync(int extensionId, int rentalId, string ownerId, string? itemTitle, DateTime newEndDate, bool autoApproved)
    {
        await hub.Clients.User(ownerId).SendAsync(RentMateHub.ExtensionRequestedEvent, new
        {
            extensionId,
            rentalId,
            itemTitle,
            newEndDate = newEndDate.ToString("yyyy-MM-dd"),
            autoApproved
        });

        await notifications.CreateAsync(
            ownerId, NotificationType.ExtensionRequested,
            localizer["Notification.ExtensionRequested"].Value,
            string.Format(localizer["NotificationMsg.ExtensionRequested"].Value,
                itemTitle ?? "", newEndDate.ToString("dd MMM")),
            extensionId, "Extension", "/Dashboard?tab=lending");
    }

    public async Task ExtensionApprovedAsync(int extensionId, string renterId, string? itemTitle, DateTime newEndDate, decimal additionalCost)
    {
        await hub.Clients.User(renterId).SendAsync(RentMateHub.ExtensionStatusChangedEvent, new
        {
            extensionId,
            status = "Accepted",
            itemTitle,
            newEndDate = newEndDate.ToString("yyyy-MM-dd"),
            additionalCost
        });

        await notifications.CreateAsync(
            renterId, NotificationType.ExtensionApproved,
            localizer["Notification.ExtensionApproved"].Value,
            string.Format(localizer["NotificationMsg.ExtensionApproved"].Value, itemTitle ?? ""),
            extensionId, "Extension", "/Dashboard?tab=renting");

        await notifications.AutoDismissAsync(extensionId, "Extension", NotificationType.ExtensionRequested);
    }

    public async Task ExtensionDeclinedAsync(int extensionId, string renterId, string? itemTitle)
    {
        await hub.Clients.User(renterId).SendAsync(RentMateHub.ExtensionStatusChangedEvent,
            new { extensionId, status = "Declined", itemTitle });

        await notifications.CreateAsync(
            renterId, NotificationType.ExtensionDeclined,
            localizer["Notification.ExtensionDeclined"].Value,
            string.Format(localizer["NotificationMsg.ExtensionDeclined"].Value, itemTitle ?? ""),
            extensionId, "Extension", "/Dashboard?tab=renting");

        await notifications.AutoDismissAsync(extensionId, "Extension", NotificationType.ExtensionRequested);
    }

    public async Task ExtensionCancelledAsync(int extensionId, string ownerId, string? itemTitle)
    {
        await hub.Clients.User(ownerId).SendAsync(RentMateHub.ExtensionStatusChangedEvent,
            new { extensionId, status = "CancelledByRenter", itemTitle });

        await notifications.CreateAsync(
            ownerId, NotificationType.ExtensionCancelled,
            localizer["Notification.ExtensionCancelled"].Value,
            string.Format(localizer["NotificationMsg.ExtensionCancelled"].Value, itemTitle ?? ""),
            extensionId, "Extension", "/Dashboard?tab=lending");

        await notifications.AutoDismissAsync(extensionId, "Extension", NotificationType.ExtensionRequested);
    }

    public async Task ExtensionPaidAsync(int extensionId, string ownerId, string? itemTitle, DateTime newEndDate)
    {
        await hub.Clients.User(ownerId).SendAsync(RentMateHub.ExtensionStatusChangedEvent, new
        {
            extensionId,
            status = "Paid",
            itemTitle,
            newEndDate = newEndDate.ToString("yyyy-MM-dd")
        });

        await notifications.CreateAsync(
            ownerId, NotificationType.ExtensionPaid,
            localizer["Notification.ExtensionPaid"].Value,
            string.Format(localizer["NotificationMsg.ExtensionPaid"].Value, itemTitle ?? ""),
            extensionId, "Extension", "/Dashboard?tab=lending");

        await notifications.AutoDismissAsync(extensionId, "Extension", NotificationType.ExtensionApproved);
    }
}
