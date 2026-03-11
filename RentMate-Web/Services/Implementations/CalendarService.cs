using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Shared.Contracts.Responses;

using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations
{
    #region DTOs
    /// <summary>
    /// Represents a blocked date range for calendar display. Immutable record for thread safety.
    /// </summary>
    public readonly record struct BlockedDateRange(string From, string To)
    {
        public static BlockedDateRange FromDates(DateOnly from, DateOnly to) =>
            new(from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

        public static BlockedDateRange FromDateTimes(DateTime from, DateTime to) =>
            FromDates(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
    }

    /// <summary>
    /// Availability data for an item, used in bulk operations.
    /// </summary>
    public readonly record struct ItemAvailability(int ItemId, IReadOnlyList<BlockedDateRange> BlockedRanges);
    #endregion

    #region Interface
    /// <summary>
    /// Service interface for calendar-related operations.
    /// </summary>
    public interface ICalendarService
    {
        Task<IReadOnlyList<BlockedDateRange>> GetBlockedDateRangesAsync(int itemId, CancellationToken cancellationToken = default);
        Task<bool> IsDateRangeAvailableAsync(int itemId, DateOnly startDate, DateOnly endDate, int? excludeRentalId = null, CancellationToken cancellationToken = default);
        IAsyncEnumerable<ItemAvailability> GetItemAvailabilitiesAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken = default);
    }
    #endregion

    #region Implementation
    /// <summary>
    /// Calendar service that centralizes blocked date logic with async patterns.
    /// </summary>
    public sealed class CalendarService : ICalendarService
    {
        private readonly RentMateContext _context;
        private static readonly RentalStatus[] ActiveStatuses = [RentalStatus.Active, RentalStatus.Pending, RentalStatus.Accepted];

        public CalendarService(RentMateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<BlockedDateRange>> GetBlockedDateRangesAsync(
            int itemId,
            CancellationToken cancellationToken = default)
        {
            var today = DateTime.Today;

            // Project to DTOs directly in the query to minimize data transfer
            var blockedRanges = await _context.Rentals
                .AsNoTracking()
                .Where(r => r.ItemId == itemId)
                .Where(r => ActiveStatuses.Contains(r.Status))
                .Where(r => r.EndDate >= today) // Only future/current bookings
                .OrderBy(r => r.StartDate)
                .Select(r => new { r.StartDate, r.EndDate })
                .ToListAsync(cancellationToken);

            // Convert at the boundary - this is where DateTime -> DateOnly conversion happens
            return blockedRanges
                .Select(r => BlockedDateRange.FromDateTimes(r.StartDate, r.EndDate))
                .ToList()
                .AsReadOnly();
        }

        /// <inheritdoc />
        public async Task<bool> IsDateRangeAvailableAsync(
            int itemId,
            DateOnly startDate,
            DateOnly endDate,
            int? excludeRentalId = null,
            CancellationToken cancellationToken = default)
        {
            // Convert DateOnly to DateTime for database comparison
            // Using UTC to ensure consistent behavior across timezones
            var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
            var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

            var query = _context.Rentals
                .AsNoTracking()
                .Where(r => r.ItemId == itemId)
                .Where(r => ActiveStatuses.Contains(r.Status));

            // Exclude specific rental (useful for modification scenarios)
            if (excludeRentalId.HasValue)
            {
                query = query.Where(r => r.Id != excludeRentalId.Value);
            }

            // Check for overlapping bookings using standard overlap detection algorithm:
            // Two ranges [A_start, A_end] and [B_start, B_end] overlap if:
            // A_start <= B_end AND A_end >= B_start
            var hasConflict = await query
                .AnyAsync(r => 
                    r.StartDate <= endDateTime && 
                    r.EndDate >= startDateTime, 
                    cancellationToken);

            return !hasConflict;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Uses IAsyncEnumerable to stream results for memory efficiency.
        /// This is particularly useful when loading availability for many items
        /// (e.g., marketplace listing with 100+ items).
        /// 
        /// Performance benefit: Instead of loading all blocked dates into memory at once,
        /// this streams item by item, keeping memory footprint constant regardless of dataset size.
        /// </remarks>
        public async IAsyncEnumerable<ItemAvailability> GetItemAvailabilitiesAsync(
            IEnumerable<int> itemIds,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var today = DateTime.Today;
            var itemIdList = itemIds.ToList();

            // Single query to fetch all relevant rentals, grouped by item
            var rentalsByItem = await _context.Rentals
                .AsNoTracking()
                .Where(r => itemIdList.Contains(r.ItemId))
                .Where(r => ActiveStatuses.Contains(r.Status))
                .Where(r => r.EndDate >= today)
                .GroupBy(r => r.ItemId)
                .Select(g => new
                {
                    ItemId = g.Key,
                    Rentals = g.Select(r => new { r.StartDate, r.EndDate }).ToList()
                })
                .ToDictionaryAsync(
                    x => x.ItemId,
                    x => x.Rentals,
                    cancellationToken);

            // Stream results
            foreach (var itemId in itemIdList)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                if (rentalsByItem.TryGetValue(itemId, out var rentals))
                {
                    var blockedRanges = rentals
                        .OrderBy(r => r.StartDate)
                        .Select(r => BlockedDateRange.FromDateTimes(r.StartDate, r.EndDate))
                        .ToList()
                        .AsReadOnly();

                    yield return new ItemAvailability(itemId, blockedRanges);
                }
                else
                {
                    // Item has no blocked dates
                    yield return new ItemAvailability(itemId, Array.Empty<BlockedDateRange>());
                }
            }
        }
    }
    #endregion
}
