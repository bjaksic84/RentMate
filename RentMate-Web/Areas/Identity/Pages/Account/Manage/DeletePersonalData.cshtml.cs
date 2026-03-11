// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for deleting user's personal data and account.
    /// Supports two modes:
    /// - Delete Account: anonymizes the user but keeps their footprint (reviews, items, rental history)
    /// - Delete All Data: removes all traces from the platform
    /// </summary>
    public class DeletePersonalDataModel : BaseIdentityPageModel
    {
        #region Constants

        private const string IncorrectPasswordError = "Incorrect password.";
        private const string DeleteErrorMessage = "Unexpected error occurred deleting user.";

        #endregion

        #region Dependencies

        private readonly ILogger<DeletePersonalDataModel> _logger;
        private readonly RentMateContext _context;
        private readonly IFileUploadService _fileUploadService;

        #endregion

        #region Constructor

        public DeletePersonalDataModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<DeletePersonalDataModel> logger,
            RentMateContext context,
            IFileUploadService fileUploadService)
            : base(userManager, signInManager)
        {
            _logger = logger;
            _context = context;
            _fileUploadService = fileUploadService;
        }

        #endregion

        #region Properties

        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>Whether password confirmation is required for deletion.</summary>
        public bool RequirePassword { get; set; }

        /// <summary>Whether the user has active rentals blocking deletion.</summary>
        public bool HasActiveRentals { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            /// When true, performs full data deletion (reviews, items, rentals removed).
            /// When false, only anonymizes the account (footprint preserved).
            /// </summary>
            public bool DeleteAllData { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGet()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            RequirePassword = await UserManager.HasPasswordAsync(user);
            HasActiveRentals = await HasActiveRentalsAsync(user.Id);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            RequirePassword = await UserManager.HasPasswordAsync(user);

            if (await HasActiveRentalsAsync(user.Id))
            {
                HasActiveRentals = true;
                ModelState.AddModelError(string.Empty,
                    "Cannot delete account while you have active, pending, or accepted rentals. " +
                    "Please complete or cancel all rentals first.");
                return Page();
            }

            if (!await ValidatePasswordIfRequiredAsync(user))
            {
                return Page();
            }

            if (Input.DeleteAllData)
            {
                await DeleteAllUserDataAsync(user);
            }
            else
            {
                await AnonymizeUserAccountAsync(user);
            }

            return Redirect("~/");
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Validates password if the user has one set.
        /// </summary>
        private async Task<bool> ValidatePasswordIfRequiredAsync(ApplicationUser user)
        {
            if (!RequirePassword) return true;

            var isPasswordValid = await UserManager.CheckPasswordAsync(user, Input.Password);
            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, IncorrectPasswordError);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Anonymizes the user account: clears personal data, locks the account,
        /// but preserves all their items, reviews, and rental history on the platform.
        /// Other users will see "Deleted User" where the name used to appear.
        /// </summary>
        private async Task AnonymizeUserAccountAsync(ApplicationUser user)
        {
            var userId = user.Id;

            // Delete profile picture from Cloudinary before clearing the URL
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                await _fileUploadService.DeleteFileAsync(user.ProfilePictureUrl);

            // Clear personal information, set display name to "Deleted account"
            user.FirstName = "Deleted account";
            user.LastName = null;
            user.City = null;
            user.ProfilePictureUrl = null;
            user.PhoneNumber = null;

            // Replace identifiable fields with anonymous placeholders
            var anonymousEmail = $"deleted_{userId[..8]}@deleted.rentmate.local";
            await UserManager.SetEmailAsync(user, anonymousEmail);
            await UserManager.SetUserNameAsync(user, anonymousEmail);

            // Lock the account permanently so it can't be logged into
            await UserManager.SetLockoutEnabledAsync(user, true);
            await UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            // Remove password and invalidate all sessions
            if (await UserManager.HasPasswordAsync(user))
            {
                await UserManager.RemovePasswordAsync(user);
            }
            await UserManager.UpdateSecurityStampAsync(user);

            // Delete favorites (no value in keeping these)
            var favorites = await _context.AccountItemFavorites
                .Where(f => f.AccountId == userId)
                .ToListAsync();
            _context.AccountItemFavorites.RemoveRange(favorites);
            await _context.SaveChangesAsync();

            await SignInManager.SignOutAsync();
            _logger.LogInformation("User with ID '{UserId}' anonymized their account (footprint preserved).", userId);
        }

        /// <summary>
        /// Performs full data deletion: removes all user traces from the platform,
        /// then deletes the user account entirely.
        /// </summary>
        private async Task DeleteAllUserDataAsync(ApplicationUser user)
        {
            var userId = user.Id;

            await CleanupAllUserReferencesAsync(userId);

            var result = await UserManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(DeleteErrorMessage);
            }

            await SignInManager.SignOutAsync();
            _logger.LogInformation("User with ID '{UserId}' deleted all personal data and account.", userId);
        }

        /// <summary>
        /// Checks if the user has any active/pending/accepted rentals as owner or renter.
        /// </summary>
        private async Task<bool> HasActiveRentalsAsync(string userId)
        {
            return await _context.Rentals.AnyAsync(r =>
                (r.RenterId == userId || r.OwnerId == userId) &&
                r.Status != RentalStatus.Completed &&
                r.Status != RentalStatus.Cancelled);
        }

        /// <summary>
        /// Removes or nullifies all foreign key references to the user
        /// that use Restrict delete behavior, so UserManager.DeleteAsync can succeed.
        /// Deletes reviews, items are cascade-deleted, rental history cleaned up.
        /// </summary>
        private async Task CleanupAllUserReferencesAsync(string userId)
        {
            // 0. Clean up all Cloudinary images before EF cascade deletes the records
            await CleanupCloudinaryImagesAsync(userId);

            // 1. Delete all reviews by this user (full data wipe)
            var reviews = await _context.Reviews
                .Where(r => r.ReviewerId == userId)
                .ToListAsync();
            _context.Reviews.RemoveRange(reviews);

            // 2. Delete rentals where user was the RENTER (non-nullable FK, Restrict).
            //    Rental sub-entities (Deposit, Accessories, Extensions) cascade-delete from Rental.
            var renterRentals = await _context.Rentals
                .Where(r => r.RenterId == userId)
                .ToListAsync();
            _context.Rentals.RemoveRange(renterRentals);

            // 3. Nullify payments where user was the payer (nullable FK, Restrict).
            var payments = await _context.Payments
                .Where(p => p.UserId == userId)
                .ToListAsync();
            foreach (var payment in payments)
                payment.UserId = null;

            // 4. Delete extension requests by this user that weren't cascade-deleted
            //    via renter rentals above (e.g., orphaned records). Non-nullable FK, Restrict.
            var extensions = await _context.RentalExtensions
                .Where(e => e.RequestedByUserId == userId)
                .ToListAsync();
            _context.RentalExtensions.RemoveRange(extensions);

            await _context.SaveChangesAsync();

            // Items (Cascade from User) and their sub-entities are handled automatically
            // by EF Core when UserManager.DeleteAsync is called.
            _logger.LogInformation("Cleaned up all data references for user {UserId} before full deletion.", userId);
        }

        /// <summary>
        /// Deletes all Cloudinary images associated with the user before EF cascade
        /// removes the database records (which would orphan the cloud files).
        /// </summary>
        private async Task CleanupCloudinaryImagesAsync(string userId)
        {
            var urlsToDelete = new List<string>();

            // Profile picture
            var user = await _context.Users.FindAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.ProfilePictureUrl))
                urlsToDelete.Add(user.ProfilePictureUrl);

            // Item images (multi-image system + legacy single-image field)
            var userItems = await _context.Items
                .Include(i => i.Images)
                .Where(i => i.UserId == userId)
                .ToListAsync();

            foreach (var item in userItems)
            {
                urlsToDelete.AddRange(item.Images.Select(img => img.ImageUrl));
                if (!string.IsNullOrEmpty(item.ImageUrl) && !urlsToDelete.Contains(item.ImageUrl))
                    urlsToDelete.Add(item.ImageUrl);
            }

            // Dispute evidence on rentals where user was renter
            var renterEvidenceUrls = await _context.DisputeEvidences
                .Where(e => e.RentalDeposit.Rental.RenterId == userId)
                .Select(e => e.Url)
                .ToListAsync();
            urlsToDelete.AddRange(renterEvidenceUrls);

            // Dispute evidence on rentals for user's items (items cascade-delete from user)
            var itemIds = userItems.Select(i => i.Id).ToList();
            var ownerEvidenceUrls = await _context.DisputeEvidences
                .Where(e => itemIds.Contains(e.RentalDeposit.Rental.ItemId))
                .Select(e => e.Url)
                .ToListAsync();
            urlsToDelete.AddRange(ownerEvidenceUrls);

            // Delete all collected URLs from Cloudinary
            var distinctUrls = urlsToDelete.Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();
            if (distinctUrls.Count > 0)
            {
                await _fileUploadService.DeleteFilesAsync(distinctUrls);
                _logger.LogInformation(
                    "Deleted {Count} Cloudinary images for user {UserId} during account deletion.",
                    distinctUrls.Count, userId);
            }
        }

        #endregion
    }
}
