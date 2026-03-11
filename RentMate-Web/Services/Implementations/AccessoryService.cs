using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services.Implementations
{
    /// <summary>
    /// Manages item accessories and their attachment to rentals with price snapshotting.
    /// </summary>
    public class AccessoryService : IAccessoryService
    {
        private readonly RentMateContext _context;
        private readonly ILogger<AccessoryService> _logger;

        public AccessoryService(
            RentMateContext context,
            ILogger<AccessoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<ItemAccessory>> GetAccessoriesForItemAsync(int itemId)
        {
            return await _context.ItemAccessories
                .AsNoTracking()
                .Where(a => a.ItemId == itemId)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<ItemAccessory> AddAccessoryAsync(
            int itemId, string name, decimal dailyPrice, string? description)
        {
            var itemExists = await _context.Items.AnyAsync(i => i.Id == itemId);
            if (!itemExists)
                throw new InvalidOperationException($"Item {itemId} not found.");

            var accessory = new ItemAccessory
            {
                ItemId = itemId,
                Name = name,
                DailyPrice = dailyPrice,
                Description = description,
                IsAvailable = true
            };

            _context.ItemAccessories.Add(accessory);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Accessory '{Name}' added to item {ItemId} at \u20ac{Price:N2}/day",
                name, itemId, dailyPrice);

            return accessory;
        }

        /// <inheritdoc/>
        public async Task<ItemAccessory> UpdateAccessoryAsync(
            int accessoryId, string ownerUserId, string name, decimal dailyPrice, bool isAvailable, string? description)
        {
            var accessory = await _context.ItemAccessories
                .FirstOrDefaultAsync(a => a.Id == accessoryId)
                ?? throw new InvalidOperationException($"Accessory {accessoryId} not found.");

            var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == accessory.ItemId);
            if (item?.UserId != ownerUserId)
                throw new UnauthorizedAccessException("Only the item owner can modify accessories.");

            accessory.Name = name;
            accessory.DailyPrice = dailyPrice;
            accessory.IsAvailable = isAvailable;
            accessory.Description = description;
            accessory.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Accessory {AccessoryId} updated: '{Name}', \u20ac{Price:N2}/day, Available={IsAvailable}",
                accessoryId, name, dailyPrice, isAvailable);

            return accessory;
        }

        /// <inheritdoc/>
        public async Task DeleteAccessoryAsync(int accessoryId, string ownerUserId)
        {
            var accessory = await _context.ItemAccessories
                .FirstOrDefaultAsync(a => a.Id == accessoryId)
                ?? throw new InvalidOperationException($"Accessory {accessoryId} not found.");

            var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == accessory.ItemId);
            if (item?.UserId != ownerUserId)
                throw new UnauthorizedAccessException("Only the item owner can delete accessories.");

            // Check if attached to any active rental
            var hasActiveRentals = await _context.RentalAccessories
                .AnyAsync(ra =>
                    ra.ItemAccessoryId == accessoryId
                    && ra.Rental!.Status != RentalStatus.Completed
                    && ra.Rental!.Status != RentalStatus.Cancelled);

            if (hasActiveRentals)
                throw new InvalidOperationException(
                    "Cannot delete accessory — it is attached to an active rental. Mark it as unavailable instead.");

            _context.ItemAccessories.Remove(accessory);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Accessory {AccessoryId} '{Name}' deleted from item {ItemId}",
                accessoryId, accessory.Name, accessory.ItemId);
        }

        /// <inheritdoc/>
        public async Task<List<RentalAccessory>> AttachAccessoriesToRentalAsync(
            int rentalId, List<int> accessoryIds)
        {
            if (accessoryIds == null || accessoryIds.Count == 0)
                return new List<RentalAccessory>();

            var rental = await _context.Rentals
                .FirstOrDefaultAsync(r => r.Id == rentalId)
                ?? throw new InvalidOperationException($"Rental {rentalId} not found.");

            var accessories = await _context.ItemAccessories
                .Where(a => accessoryIds.Contains(a.Id) && a.IsAvailable)
                .ToListAsync();

            if (accessories.Count != accessoryIds.Count)
            {
                _logger.LogWarning(
                    "Some accessories not found or unavailable for rental {RentalId}. Requested: {Requested}, Found: {Found}",
                    rentalId, accessoryIds.Count, accessories.Count);
            }

            var rentalAccessories = accessories.Select(a => new RentalAccessory
            {
                RentalId = rentalId,
                ItemAccessoryId = a.Id,
                Name = a.Name,
                DailyPrice = a.DailyPrice
            }).ToList();

            _context.RentalAccessories.AddRange(rentalAccessories);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Attached {Count} accessories to rental {RentalId}",
                rentalAccessories.Count, rentalId);

            return rentalAccessories;
        }
    }
}
