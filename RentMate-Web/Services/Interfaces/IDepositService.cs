using RentMate.Models.Domain;

namespace RentMate.Services.Interfaces
{
    /// <summary>
    /// Manages rental deposit lifecycle: creation, authorization, release, and charging.
    /// </summary>
    public interface IDepositService
    {
        /// <summary>
        /// Creates a deposit record for a rental and initiates authorization.
        /// </summary>
        Task<RentalDeposit> CreateAndAuthorizeDepositAsync(int rentalId, decimal amount);

        /// <summary>
        /// Releases the full deposit back to the renter (item returned in good condition).
        /// </summary>
        Task<RentalDeposit> ReleaseDepositAsync(int rentalId, string releasedByUserId);

        /// <summary>
        /// Charges a partial or full amount from the deposit (damage, non-return, etc.).
        /// </summary>
        Task<RentalDeposit> ChargeDepositAsync(int rentalId, decimal amount, string reason, string chargedByUserId);

        /// <summary>
        /// Disputes a charged deposit (renter action).
        /// </summary>
        Task<RentalDeposit> DisputeDepositAsync(int rentalId, string reason, string disputedByUserId);

        /// <summary>
        /// Releases a disputed deposit back to the renter (owner action).
        /// </summary>
        Task<RentalDeposit> ReleaseDisputedDepositAsync(int rentalId, string ownerUserId);

        /// <summary>
        /// Renter accepts the deposit charge (clears deadline, finalizes charge).
        /// </summary>
        Task<RentalDeposit> AcceptChargeAsync(int rentalId, string renterUserId);

        /// <summary>
        /// Owner makes a counter-offer with a lower amount during a dispute.
        /// </summary>
        Task<RentalDeposit> CounterOfferDepositAsync(int rentalId, decimal amount, string response, string ownerUserId);

        /// <summary>
        /// Escalates a disputed deposit to admin review (either party action).
        /// </summary>
        Task<RentalDeposit> EscalateDisputeAsync(int rentalId, string userId, string? response = null);

        /// <summary>
        /// Renter accepts the owner's counter-offer.
        /// </summary>
        Task<RentalDeposit> AcceptCounterOfferAsync(int rentalId, string renterUserId);

        /// <summary>
        /// Renter rejects the counter-offer; original charge stands.
        /// </summary>
        Task<RentalDeposit> RejectCounterOfferAsync(int rentalId, string renterUserId);

        /// <summary>
        /// Admin resolves an escalated dispute with a final decision on the charge amount.
        /// </summary>
        /// <param name="rentalId">ID of the rental in dispute.</param>
        /// <param name="amount">The final amount to be charged (0 for full release).</param>
        /// <param name="adminNotes">Notes explaining the decision.</param>
        /// <param name="adminUserId">ID of the administrator resolving the dispute.</param>
        Task<RentalDeposit> AdminResolveDisputeAsync(int rentalId, decimal amount, string adminNotes, string adminUserId);

        /// <summary>
        /// Gets all escalated disputes awaiting admin review.
        /// </summary>
        Task<List<RentalDeposit>> GetEscalatedDisputesAsync();

        /// <summary>
        /// Gets all resolved disputes for archive review.
        /// </summary>
        Task<List<RentalDeposit>> GetResolvedDisputesAsync();

        /// <summary>
        /// Gets the deposit for a specific rental.
        /// </summary>
        Task<RentalDeposit?> GetDepositForRentalAsync(int rentalId);

        /// <summary>
        /// Gets aggregate deposit information for an owner's active rentals.
        /// </summary>
        Task<DepositSummary> GetDepositSummaryForOwnerAsync(string ownerUserId);

        /// <summary>
        /// Uploads photographic or document evidence for a dispute.
        /// </summary>
        Task<DisputeEvidence> UploadEvidenceAsync(int rentalId, string userId, Microsoft.AspNetCore.Http.IFormFile file, string? notes);
    }

    /// <summary>
    /// Summary of deposit information for dashboard display.
    /// </summary>
    public class DepositSummary
    {
        public decimal TotalHeld { get; set; }
        public int ActiveDepositCount { get; set; }
        public decimal TotalCharged { get; set; }
        public decimal TotalReleased { get; set; }
    }
}
