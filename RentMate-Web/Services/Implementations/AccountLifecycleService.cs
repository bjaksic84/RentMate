using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Services.Implementations;

/// <summary>
/// Centralises account lifecycle operations: deactivation, reactivation, and GDPR deletion.
/// </summary>
public class AccountLifecycleService : IAccountLifecycleService
{
    /// <summary>
    /// Email suffix applied to all anonymised (deleted) user accounts.
    /// Used by <see cref="DataRetentionService"/> to identify tombstone records.
    /// </summary>
    public const string AnonymisedEmailSuffix = "@deleted.rentmate";

    /// <summary>
    /// Email prefix applied to all anonymised (deleted) user accounts.
    /// </summary>
    public const string AnonymisedEmailPrefix = "deleted_";

    private readonly RentMateContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileUploadService _fileUploadService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<AccountLifecycleService> _logger;

    public AccountLifecycleService(
        RentMateContext context,
        UserManager<ApplicationUser> userManager,
        IFileUploadService fileUploadService,
        IPaymentService paymentService,
        ILogger<AccountLifecycleService> logger)
    {
        _context = context;
        _userManager = userManager;
        _fileUploadService = fileUploadService;
        _paymentService = paymentService;
        _logger = logger;
    }

    #region Public API

    /// <inheritdoc/>
    public async Task<bool> HasActiveRentalsAsync(string userId)
    {
        return await _context.Rentals.AnyAsync(r =>
            (r.RenterId == userId || r.OwnerId == userId) &&
            r.Status != RentalStatus.Completed &&
            r.Status != RentalStatus.Cancelled);
    }

    /// <inheritdoc/>
    public async Task DeactivateAccountAsync(string userId, DeactivationSource source, string? reason = null)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        user.IsDeactivated = true;
        user.DeactivatedAt = DateTime.UtcNow;
        user.DeactivatedBy = source;
        user.DeactivationReason = reason != null && reason.Length > 500 ? reason[..500] : reason;

        // Delist all items so they vanish from the marketplace
        var items = await _context.Items.Where(i => i.UserId == userId).ToListAsync();
        foreach (var item in items)
            item.IsListed = false;

        // Invalidate all existing sessions
        await _userManager.UpdateSecurityStampAsync(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Account {UserId} deactivated by {Source}.", userId, source);
    }

    /// <inheritdoc/>
    public async Task ReactivateAccountAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        user.IsDeactivated = false;
        user.DeactivatedAt = null;
        user.DeactivatedBy = null;
        user.DeactivationReason = null;

        // Re-list items that are not admin-hidden
        var items = await _context.Items
            .Where(i => i.UserId == userId && !i.IsAdminHidden)
            .ToListAsync();
        foreach (var item in items)
            item.IsListed = true;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Account {UserId} reactivated.", userId);
    }

    /// <inheritdoc/>
    public async Task DeleteAccountAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (await HasActiveRentalsAsync(userId))
            throw new InvalidOperationException(
                "Cannot delete account while active, pending, or accepted rentals exist.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await CleanupCloudinaryImagesAsync(userId, user.ProfilePictureUrl);
            await CleanupStripeCustomerAsync(user.Email ?? string.Empty);
            await AnonymiseUserDataAsync(user, userId);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("Account {UserId} permanently deleted (PII anonymised).", userId);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Anonymises all PII on the user record and removes related owned data.
    /// Rental records (as renter/owner) are preserved — they reference the now-anonymised user.
    /// </summary>
    private async Task AnonymiseUserDataAsync(ApplicationUser user, string userId)
    {
        // --- Hard-delete items (cascade removes images, accessories, favorites on those items) ---
        // Items cannot exist without an active owner; rental history on those items is preserved
        // because Item → Rentals FK is Cascade, meaning rentals belonging to deleted items are
        // removed too.  This is intentional: the item is gone and so are its pending rentals.
        // Active/completed rentals are blocked by HasActiveRentalsAsync above.
        var items = await _context.Items.Include(i => i.Images).Where(i => i.UserId == userId).ToListAsync();
        _context.Items.RemoveRange(items);

        // --- Nullify Payment.UserId (preserve financial audit trail) ---
        var payments = await _context.Payments.Where(p => p.UserId == userId).ToListAsync();
        foreach (var payment in payments)
            payment.UserId = null;

        // --- Remove favorites ---
        var favorites = await _context.AccountItemFavorites.Where(f => f.AccountId == userId).ToListAsync();
        _context.AccountItemFavorites.RemoveRange(favorites);

        await _context.SaveChangesAsync();

        // --- Anonymise the user record itself ---
        user.FirstName = "Deleted User";
        user.LastName = null;
        user.City = null;
        user.ProfilePictureUrl = null;
        user.PhoneNumber = null;
        user.Bio = null;
        user.CategoryAffinityJson = null;
        user.Latitude = null;
        user.Longitude = null;
        user.HasPaymentMethodAdded = false;
        user.IsPhoneVerified = false;
        user.IsGovernmentIdVerified = false;
        user.IsSocialMediaLinked = false;
        user.IsDeactivated = false;
        user.DeactivatedAt = null;
        user.DeactivatedBy = null;
        user.DeactivationReason = null;
        user.PrivacyPolicyAcceptedAt = null;
        user.PrivacyPolicyVersion = null;
        user.ResponseRate = 0;
        user.AvgResponseTimeHours = 0;
        user.TotalMessagesReceived = 0;
        user.ProfileTrustScore = 0;
        user.ProfileTrustScoreUpdatedAt = null;
        user.NotifyOnRentalRequest = false;
        user.NotifyOnMessage = false;
        user.NotifyOnReview = false;
        user.NotifyOnRentalStatusChange = false;
        user.OnboardingCompleted = false;
        user.PreferredLanguage = "sl";

        // Use a GUID-based anonymous email to avoid leaking the original user ID
        var anonymousEmail = AnonymisedEmailPrefix + $"{Guid.NewGuid():N}"[..12] + AnonymisedEmailSuffix;
        await _userManager.SetEmailAsync(user, anonymousEmail);
        await _userManager.SetUserNameAsync(user, anonymousEmail);

        // Lock the account permanently
        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        if (await _userManager.HasPasswordAsync(user))
            await _userManager.RemovePasswordAsync(user);

        await _userManager.UpdateSecurityStampAsync(user);
    }

    /// <summary>
    /// Gathers all Cloudinary URLs associated with the user and deletes them in bulk.
    /// </summary>
    private async Task CleanupCloudinaryImagesAsync(string userId, string? profilePictureUrl)
    {
        var urls = new List<string>();

        if (!string.IsNullOrEmpty(profilePictureUrl))
            urls.Add(profilePictureUrl);

        var items = await _context.Items
            .Include(i => i.Images)
            .Where(i => i.UserId == userId)
            .ToListAsync();

        foreach (var item in items)
        {
            urls.AddRange(item.Images.Select(img => img.ImageUrl).Where(u => !string.IsNullOrEmpty(u))!);
            if (!string.IsNullOrEmpty(item.ImageUrl) && !urls.Contains(item.ImageUrl))
                urls.Add(item.ImageUrl);
        }

        // Dispute evidence uploaded by this user as a renter
        var renterEvidence = await _context.DisputeEvidences
            .Where(e => e.RentalDeposit != null && e.RentalDeposit.Rental != null && e.RentalDeposit.Rental.RenterId == userId)
            .Select(e => e.Url)
            .ToListAsync();
        urls.AddRange(renterEvidence.Where(u => !string.IsNullOrEmpty(u)).Select(u => u!));

        // Dispute evidence on rentals of items owned by this user
        var itemIds = items.Select(i => i.Id).ToList();
        var ownerEvidence = await _context.DisputeEvidences
            .Where(e => e.RentalDeposit != null && e.RentalDeposit.Rental != null && itemIds.Contains(e.RentalDeposit.Rental.ItemId))
            .Select(e => e.Url)
            .ToListAsync();
        urls.AddRange(ownerEvidence.Where(u => !string.IsNullOrEmpty(u)).Select(u => u!));

        var distinct = urls.Distinct().ToList();
        if (distinct.Count > 0)
        {
            try
            {
                await _fileUploadService.DeleteFilesAsync(distinct);
                _logger.LogInformation("Deleted {Count} Cloudinary assets for user {UserId}.", distinct.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloudinary cleanup partially failed for user {UserId}.", userId);
            }
        }
    }

    /// <summary>Deletes the Stripe customer record. Non-fatal on failure.</summary>
    private async Task CleanupStripeCustomerAsync(string email)
    {
        try
        {
            await _paymentService.DeleteCustomerAsync(email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Stripe customer for {Email}.", email);
        }
    }

    #endregion
}
