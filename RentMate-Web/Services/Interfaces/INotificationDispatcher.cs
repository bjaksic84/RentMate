using RentMate.Models.Domain;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services.Interfaces
{
    /// <summary>
    /// Dispatches SignalR hub events and persistent notifications together in one call.
    /// Eliminates the repeated hub.SendAsync + notificationService.CreateAsync pairs
    /// across DisputeController, DashboardController, RentalsController, and PaymentController.
    /// </summary>
    public interface INotificationDispatcher
    {
        // ── Rental lifecycle events ───────────────────────────────────

        /// <summary>
        /// Notifies the owner when a renter submits a new rental request.
        /// </summary>
        Task RentalRequestedAsync(int rentalId, string ownerId, string? itemTitle,
            string? renterEmail, string? renterName, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Notifies the renter when the owner changes rental status (Accepted/Completed/Cancelled).
        /// </summary>
        Task RentalStatusChangedAsync(int rentalId, string renterId, string? itemTitle,
            RentalStatus status, string? message = null);

        // ── Deposit events ────────────────────────────────────────────

        Task DepositReleasedAsync(int rentalId, string renterId, string? itemTitle);

        Task DepositChargedAsync(int rentalId, string renterId, string? itemTitle, decimal? amount = null, string? reason = null);

        Task DepositDisputedAsync(int rentalId, string ownerId, string? itemTitle);

        Task DepositCounterOfferedAsync(int rentalId, string renterId, string? itemTitle);

        Task DepositResolvedAsync(int rentalId, string recipientId, string? itemTitle, string status = "ChargeAccepted");

        Task DepositEscalatedAsync(int rentalId, string recipientId, string? itemTitle);

        Task DepositCounterRejectedAsync(int rentalId, string ownerId, string? itemTitle, bool escalated);

        /// <summary>
        /// Notifies both owner and renter (admin resolution path).
        /// </summary>
        Task DepositAdminResolvedAsync(int rentalId, string ownerId, string renterId, string? itemTitle, decimal amount, string? adminNotes = null);

        // ── Extension events ──────────────────────────────────────────

        Task ExtensionRequestedAsync(int extensionId, int rentalId, string ownerId, string? itemTitle, DateTime newEndDate, bool autoApproved);

        Task ExtensionApprovedAsync(int extensionId, string renterId, string? itemTitle, DateTime newEndDate, decimal additionalCost);

        Task ExtensionDeclinedAsync(int extensionId, string renterId, string? itemTitle);

        Task ExtensionCancelledAsync(int extensionId, string ownerId, string? itemTitle);

        Task ExtensionPaidAsync(int extensionId, string ownerId, string? itemTitle, DateTime newEndDate);
    }
}
