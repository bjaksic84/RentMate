using RentMate.Models.Domain;

namespace RentMate.Services.Interfaces
{
    /// <summary>
    /// Manages rental extension requests, approvals, and auto-approve logic.
    /// </summary>
    public interface IRentalExtensionService
    {
        /// <summary>
        /// Creates an extension request. May auto-approve if the item allows it and no conflicts exist.
        /// </summary>
        Task<RentalExtension> RequestExtensionAsync(int rentalId, string requestedByUserId, DateTime newEndDate);

        /// <summary>
        /// Approves a pending extension request (owner action).
        /// </summary>
        Task<RentalExtension> ApproveExtensionAsync(int extensionId, string approvedByUserId);

        /// <summary>
        /// Declines a pending extension request (owner action).
        /// </summary>
        Task<RentalExtension> DeclineExtensionAsync(int extensionId, string declinedByUserId);

        /// <summary>
        /// Checks if a rental can be extended to the proposed date (no scheduling conflicts).
        /// </summary>
        Task<bool> CanExtendAsync(int rentalId, DateTime proposedEndDate);

        /// <summary>
        /// Gets the earliest date when another rental starts for the same item (scheduling boundary).
        /// </summary>
        Task<DateTime?> GetEarliestConflictDateAsync(int itemId, DateTime afterDate);

        /// <summary>
        /// Gets all pending extension requests for items owned by this user.
        /// </summary>
        Task<List<RentalExtension>> GetPendingExtensionsForOwnerAsync(string ownerUserId);

        /// <summary>
        /// Finalizes an accepted extension after payment. Updates rental dates and price.
        /// </summary>
        Task<RentalExtension> FinalizeExtensionAsync(int extensionId, string userId);

        /// <summary>
        /// Cancels an accepted extension (renter decided not to pay).
        /// </summary>
        Task<RentalExtension> CancelExtensionAsync(int extensionId, string userId);
    }
}
