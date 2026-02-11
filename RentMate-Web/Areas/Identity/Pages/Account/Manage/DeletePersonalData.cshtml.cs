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

        #endregion

        #region Constructor

        public DeletePersonalDataModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<DeletePersonalDataModel> logger,
            RentMateContext context)
            : base(userManager, signInManager)
        {
            _logger = logger;
            _context = context;
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

        #endregion
    }
}
