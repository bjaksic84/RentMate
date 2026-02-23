using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services.Implementations
{
    /// <summary>
    /// Manages rental extension requests with approval flow and scheduling conflict detection.
    /// </summary>
    public class RentalExtensionService : IRentalExtensionService
    {
        private readonly RentMateContext _context;
        private readonly ILogger<RentalExtensionService> _logger;

        public RentalExtensionService(
            RentMateContext context,
            ILogger<RentalExtensionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<RentalExtension> RequestExtensionAsync(
            int rentalId, string requestedByUserId, DateTime newEndDate)
        {
            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId)
                ?? throw new InvalidOperationException($"Rental {rentalId} not found.");

            if (rental.Status != RentalStatus.Active)
                throw new InvalidOperationException("Can only extend active rentals.");

            if (rental.RenterId != requestedByUserId)
                throw new UnauthorizedAccessException("Only the renter can request an extension.");

            if (newEndDate <= rental.EndDate)
                throw new ArgumentException("New end date must be after current end date.");

            // Check max rental days constraint
            if (rental.Item?.MaxRentalDays.HasValue == true)
            {
                var totalDays = (newEndDate - rental.StartDate).Days;
                if (totalDays > rental.Item.MaxRentalDays.Value)
                    throw new InvalidOperationException(
                        $"Extension would exceed maximum rental duration of {rental.Item.MaxRentalDays} days.");
            }

            // Check for scheduling conflicts
            if (!await CanExtendAsync(rentalId, newEndDate))
                throw new InvalidOperationException(
                    "Cannot extend — another rental is scheduled for this item during the requested period.");

            // Check for existing pending extension
            var hasPending = await _context.RentalExtensions
                .AnyAsync(e => e.RentalId == rentalId && e.Status == ExtensionStatus.Pending);
            if (hasPending)
                throw new InvalidOperationException("A pending extension request already exists for this rental.");

            var dailyRate = rental.Item?.Price ?? 0;
            var extraDays = (newEndDate - rental.EndDate).Days;

            var extension = new RentalExtension
            {
                RentalId = rentalId,
                RequestedByUserId = requestedByUserId,
                OriginalEndDate = rental.EndDate,
                NewEndDate = newEndDate,
                DailyRate = dailyRate,
                AdditionalCost = dailyRate * extraDays,
                Status = ExtensionStatus.Pending
            };

            // Auto-approve if the item allows it
            if (rental.Item?.AutoApproveExtensions == true)
            {
                extension.Status = ExtensionStatus.AutoApproved;
                _logger.LogInformation(
                    "Extension auto-approved for rental {RentalId}: {OldDate} → {NewDate} (Awaiting payment)",
                    rentalId, extension.OriginalEndDate, newEndDate);
            }

            _context.RentalExtensions.Add(extension);
            await _context.SaveChangesAsync();

            return extension;
        }

        /// <inheritdoc/>
        public async Task<RentalExtension> ApproveExtensionAsync(int extensionId, string approvedByUserId)
        {
            var extension = await _context.RentalExtensions
                .Include(e => e.Rental)
                .FirstOrDefaultAsync(e => e.Id == extensionId)
                ?? throw new InvalidOperationException($"Extension {extensionId} not found.");

            if (extension.Status != ExtensionStatus.Pending)
                throw new InvalidOperationException($"Extension is not pending (status: {extension.Status}).");

            if (extension.Rental?.OwnerId != approvedByUserId)
                throw new UnauthorizedAccessException("Only the item owner can approve extensions.");

            // Re-check for conflicts (may have changed since request)
            if (!await CanExtendAsync(extension.RentalId, extension.NewEndDate))
                throw new InvalidOperationException(
                    "Cannot approve — a scheduling conflict now exists for the requested period.");

            extension.Status = ExtensionStatus.Accepted;
            extension.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Extension {ExtensionId} accepted (awaiting payment) for rental {RentalId} by owner {OwnerId}",
                extensionId, extension.RentalId, approvedByUserId);

            return extension;
        }

        /// <inheritdoc/>
        public async Task<RentalExtension> DeclineExtensionAsync(int extensionId, string declinedByUserId)
        {
            var extension = await _context.RentalExtensions
                .Include(e => e.Rental)
                .FirstOrDefaultAsync(e => e.Id == extensionId)
                ?? throw new InvalidOperationException($"Extension {extensionId} not found.");

            if (extension.Status != ExtensionStatus.Pending)
                throw new InvalidOperationException($"Extension is not pending (status: {extension.Status}).");

            if (extension.Rental?.OwnerId != declinedByUserId)
                throw new UnauthorizedAccessException("Only the item owner can decline extensions.");

            extension.Status = ExtensionStatus.Declined;
            extension.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Extension {ExtensionId} declined for rental {RentalId} by owner {OwnerId}",
                extensionId, extension.RentalId, declinedByUserId);

            return extension;
        }

        /// <inheritdoc/>
        public async Task<bool> CanExtendAsync(int rentalId, DateTime proposedEndDate)
        {
            var rental = await _context.Rentals
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null) return false;

            // Check if any other active/pending rental for the same item overlaps
            return !await _context.Rentals
                .AnyAsync(r =>
                    r.ItemId == rental.ItemId
                    && r.Id != rentalId
                    && r.Status != RentalStatus.Completed
                    && r.Status != RentalStatus.Cancelled
                    && r.StartDate < proposedEndDate
                    && r.EndDate > rental.EndDate);
        }

        /// <inheritdoc/>
        public async Task<DateTime?> GetEarliestConflictDateAsync(int itemId, DateTime afterDate)
        {
            return await _context.Rentals
                .AsNoTracking()
                .Where(r =>
                    r.ItemId == itemId
                    && r.StartDate > afterDate
                    && r.Status != RentalStatus.Completed
                    && r.Status != RentalStatus.Cancelled)
                .OrderBy(r => r.StartDate)
                .Select(r => (DateTime?)r.StartDate)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<RentalExtension> FinalizeExtensionAsync(int extensionId, string userId)
        {
            var extension = await _context.RentalExtensions
                .Include(e => e.Rental)
                .FirstOrDefaultAsync(e => e.Id == extensionId)
                ?? throw new InvalidOperationException($"Extension {extensionId} not found.");

            var finalizableStatuses = new[] { ExtensionStatus.Accepted, ExtensionStatus.AutoApproved };
            if (!finalizableStatuses.Contains(extension.Status))
                throw new InvalidOperationException($"Extension is not accepted or auto-approved (status: {extension.Status}).");

            if (extension.Rental?.RenterId != userId)
                throw new UnauthorizedAccessException("Only the renter can finalize an extension payment.");

            extension.Status = ExtensionStatus.Approved;
            extension.UpdatedAt = DateTime.UtcNow;

            extension.Rental!.EndDate = extension.NewEndDate;
            extension.Rental.TotalPrice += extension.AdditionalCost;
            extension.Rental.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Extension {ExtensionId} finalized (paid) for rental {RentalId}",
                extensionId, extension.RentalId);

            return extension;
        }

        /// <inheritdoc/>
        public async Task<RentalExtension> CancelExtensionAsync(int extensionId, string userId)
        {
            var extension = await _context.RentalExtensions
                .Include(e => e.Rental)
                .FirstOrDefaultAsync(e => e.Id == extensionId)
                ?? throw new InvalidOperationException($"Extension {extensionId} not found.");

            var cancellableStatuses = new[] { ExtensionStatus.Pending, ExtensionStatus.Accepted, ExtensionStatus.AutoApproved };
            if (!cancellableStatuses.Contains(extension.Status))
                throw new InvalidOperationException($"Extension is not in a cancellable state (status: {extension.Status}).");

            if (extension.Rental?.RenterId != userId)
                throw new UnauthorizedAccessException("Only the renter can cancel an extension.");

            extension.Status = ExtensionStatus.Declined;
            extension.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Extension {ExtensionId} cancelled by renter for rental {RentalId}",
                extensionId, extension.RentalId);

            return extension;
        }

        /// <inheritdoc/>
        public async Task<List<RentalExtension>> GetPendingExtensionsForOwnerAsync(string ownerUserId)
        {
            return await _context.RentalExtensions
                .AsNoTracking()
                .Include(e => e.Rental)
                    .ThenInclude(r => r!.Item)
                .Include(e => e.RequestedBy)
                .Where(e =>
                    e.Status == ExtensionStatus.Pending
                    && e.Rental!.OwnerId == ownerUserId)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync();
        }
    }
}
