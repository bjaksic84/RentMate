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
        /// Gets the deposit for a specific rental.
        /// </summary>
        Task<RentalDeposit?> GetDepositForRentalAsync(int rentalId);

        /// <summary>
        /// Gets aggregate deposit information for an owner's active rentals.
        /// </summary>
        Task<DepositSummary> GetDepositSummaryForOwnerAsync(string ownerUserId);
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
