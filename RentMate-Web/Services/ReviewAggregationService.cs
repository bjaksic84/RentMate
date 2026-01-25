using Microsoft.EntityFrameworkCore;
using RentMate.Data;

namespace RentMate.Services
{
    /// <summary>
    /// Service for managing review aggregation (count and average rating) on items.
    /// Uses atomic database operations to prevent race conditions.
    /// </summary>
    public class ReviewAggregationService : IReviewAggregationService
    {
        private readonly RentMateContext _context;
        private readonly ILogger<ReviewAggregationService> _logger;

        public ReviewAggregationService(RentMateContext context, ILogger<ReviewAggregationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateItemAggregatesAsync(int itemId)
        {
            try
            {
                // Use a single atomic query to calculate and update aggregates
                // This prevents race conditions that could occur with separate read/write operations
                var aggregates = await _context.Reviews
                    .Where(r => r.ItemId == itemId && !r.IsDeleted)
                    .GroupBy(r => r.ItemId)
                    .Select(g => new 
                    { 
                        Count = g.Count(), 
                        Average = g.Average(r => (double)r.Rating) 
                    })
                    .FirstOrDefaultAsync();

                // Use ExecuteUpdateAsync for atomic update (EF Core 7+)
                int rowsAffected;
                if (aggregates == null)
                {
                    // No reviews - reset to defaults
                    rowsAffected = await _context.Items
                        .Where(i => i.Id == itemId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(i => i.ReviewCount, 0)
                            .SetProperty(i => i.AverageRating, (double?)null)
                            .SetProperty(i => i.UpdatedAt, DateTime.UtcNow));
                }
                else
                {
                    // Update with calculated values
                    rowsAffected = await _context.Items
                        .Where(i => i.Id == itemId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(i => i.ReviewCount, aggregates.Count)
                            .SetProperty(i => i.AverageRating, Math.Round(aggregates.Average, 2))
                            .SetProperty(i => i.UpdatedAt, DateTime.UtcNow));
                }

                if (rowsAffected == 0)
                {
                    _logger.LogWarning("Failed to update aggregates - Item {ItemId} not found", itemId);
                    return false;
                }

                _logger.LogDebug("Updated aggregates for item {ItemId}: Count={Count}, Avg={Average}", 
                    itemId, 
                    aggregates?.Count ?? 0, 
                    aggregates != null ? Math.Round(aggregates.Average, 2) : (double?)null);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating aggregates for item {ItemId}", itemId);
                throw;
            }
        }
    }
}
