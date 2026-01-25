namespace RentMate.Services
{
    /// <summary>
    /// Service for managing review aggregation (count and average rating) on items.
    /// Centralizes the logic to avoid duplication across controllers.
    /// </summary>
    public interface IReviewAggregationService
    {
        /// <summary>
        /// Updates the ReviewCount and AverageRating for the specified item.
        /// Uses a transaction to ensure consistency.
        /// </summary>
        /// <param name="itemId">The ID of the item to update aggregates for.</param>
        /// <returns>True if the update was successful, false if item was not found.</returns>
        Task<bool> UpdateItemAggregatesAsync(int itemId);
    }
}
