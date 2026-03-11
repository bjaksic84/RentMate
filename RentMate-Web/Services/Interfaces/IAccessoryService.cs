using RentMate.Models.Domain;

namespace RentMate.Services.Interfaces
{
    /// <summary>
    /// Manages item accessories and their attachment to rentals.
    /// </summary>
    public interface IAccessoryService
    {
        /// <summary>
        /// Gets all accessories for an item.
        /// </summary>
        Task<List<ItemAccessory>> GetAccessoriesForItemAsync(int itemId);

        /// <summary>
        /// Adds a new accessory to an item.
        /// </summary>
        Task<ItemAccessory> AddAccessoryAsync(int itemId, string name, decimal dailyPrice, string? description);

        /// <summary>
        /// Updates an existing accessory. Verifies the caller owns the item.
        /// </summary>
        Task<ItemAccessory> UpdateAccessoryAsync(int accessoryId, string ownerUserId, string name, decimal dailyPrice, bool isAvailable, string? description);

        /// <summary>
        /// Deletes an accessory. Verifies ownership and fails if attached to an active rental.
        /// </summary>
        Task DeleteAccessoryAsync(int accessoryId, string ownerUserId);

        /// <summary>
        /// Attaches selected accessories to a rental, snapshotting current names and prices.
        /// </summary>
        Task<List<RentalAccessory>> AttachAccessoriesToRentalAsync(int rentalId, List<int> accessoryIds);
    }
}
