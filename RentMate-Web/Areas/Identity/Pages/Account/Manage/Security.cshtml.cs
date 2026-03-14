// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Shared.Contracts.Responses;
using RentMate.Services.Interfaces;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Consolidated Security page model.
    /// Merges: Email, ChangePassword/SetPassword, ExternalLogins,
    /// DownloadPersonalData, and DeletePersonalData into one page.
    /// </summary>
    public class SecurityModel : BaseIdentityPageModel
    {
        #region Constants

        private const string EmailUnchangedMessage = "Your email is unchanged.";
        private const string EmailChangeConfirmationSentMessage = "Confirmation link to change email sent. Please check your email.";
        private const string VerificationEmailSentMessage = "Verification email sent. Please check your email.";
        private const string ConfirmEmailSubject = "Confirm your email";

        private const string PasswordChangedMessage = "Your password has been changed.";

        private const string LoginRemovedMessage = "The external login was removed.";
        private const string LoginNotRemovedMessage = "The external login was not removed.";
        private const string LoginAddedMessage = "The external login was added.";
        private const string LoginNotAddedMessage = "The external login was not added. External logins can only be associated with one account.";
        private const string LoadExternalLoginError = "Unexpected error occurred loading external login info.";

        private const string IncorrectPasswordError = "Incorrect password.";
        private const string DeleteErrorMessage = "Unexpected error occurred deleting user.";

        private const string DownloadFileName = "PersonalData.json";
        private const string JsonContentType = "application/json";

        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 100;

        #endregion

        #region Dependencies

        private readonly IEmailSender _emailSender;
        private readonly ILogger<SecurityModel> _logger;
        private readonly RentMateContext _context;
        private readonly IFileUploadService _fileUploadService;
        private readonly IPaymentService _paymentService;
        private readonly IUserStore<ApplicationUser> _userStore;

        #endregion

        #region Constructor

        public SecurityModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<SecurityModel> logger,
            RentMateContext context,
            IFileUploadService fileUploadService,
            IPaymentService paymentService,
            IUserStore<ApplicationUser> userStore)
            : base(userManager, signInManager)
        {
            _emailSender = emailSender;
            _logger = logger;
            _context = context;
            _fileUploadService = fileUploadService;
            _paymentService = paymentService;
            _userStore = userStore;
        }

        #endregion

        #region Properties

        // ── Email section ─────────────────────────────────────────────────
        public string Email { get; set; }
        public bool IsEmailConfirmed { get; set; }

        // ── Password section ──────────────────────────────────────────────
        public bool HasPassword { get; set; }

        // ── External logins section ───────────────────────────────────────
        public IList<UserLoginInfo> CurrentLogins { get; set; }
        public IList<AuthenticationScheme> OtherLogins { get; set; }
        public bool ShowRemoveButton { get; set; }

        // ── Delete section ────────────────────────────────────────────────
        public bool HasActiveRentals { get; set; }
        public bool RequirePassword { get; set; }
        public bool OpenPrivacySection { get; set; }

        // ── Input models (bound per handler) ─────────────────────────────
        [BindProperty]
        public EmailInputModel EmailInput { get; set; }

        [BindProperty]
        public PasswordInputModel PasswordInput { get; set; }

        [BindProperty]
        public DeleteInputModel DeleteInput { get; set; }

        #endregion

        #region Input Models

        public class EmailInputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "New email")]
            public string NewEmail { get; set; }
        }

        public class PasswordInputModel
        {
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string OldPassword { get; set; }

            [Required]
            [StringLength(MaxPasswordLength, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = MinPasswordLength)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public class DeleteInputModel
        {
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            /// When true, performs full data deletion (all records removed).
            /// When false, only anonymizes the account (footprint preserved).
            /// </summary>
            public bool DeleteAllData { get; set; }
        }

        #endregion

        #region GET Handler

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await LoadAllSectionDataAsync(user);
            return Page();
        }

        #endregion

        #region Email Handlers

        public async Task<IActionResult> OnPostChangeEmailAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            if (!ModelState.IsValid)
            {
                await LoadAllSectionDataAsync(user);
                return Page();
            }

            var currentEmail = await UserManager.GetEmailAsync(user);
            if (EmailInput.NewEmail == currentEmail)
            {
                SetSuccessMessage(EmailUnchangedMessage);
                return RedirectToPage();
            }

            await SendEmailChangeConfirmationAsync(user, EmailInput.NewEmail);
            SetSuccessMessage(EmailChangeConfirmationSentMessage);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await SendEmailVerificationAsync(user);
            SetSuccessMessage(VerificationEmailSentMessage);
            return RedirectToPage();
        }

        #endregion

        #region Password Handler

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            if (!ModelState.IsValid)
            {
                await LoadAllSectionDataAsync(user);
                return Page();
            }

            var hasPassword = await UserManager.HasPasswordAsync(user);
            IdentityResult result;

            if (hasPassword)
            {
                // Change existing password
                if (string.IsNullOrWhiteSpace(PasswordInput.OldPassword))
                {
                    ModelState.AddModelError(string.Empty, "Current password is required.");
                    await LoadAllSectionDataAsync(user);
                    return Page();
                }
                result = await UserManager.ChangePasswordAsync(user, PasswordInput.OldPassword, PasswordInput.NewPassword);
            }
            else
            {
                // Set a new password (user logged in via external provider)
                result = await UserManager.AddPasswordAsync(user, PasswordInput.NewPassword);
            }

            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                await LoadAllSectionDataAsync(user);
                return Page();
            }

            await RefreshSignInAsync(user);
            _logger.LogInformation("User changed/set their password successfully.");
            SetSuccessMessage(PasswordChangedMessage);
            return RedirectToPage();
        }

        #endregion

        #region External Login Handlers

        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            var result = await UserManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                SetErrorMessage(LoginNotRemovedMessage);
                return RedirectToPage();
            }

            await RefreshSignInAsync(user);
            SetSuccessMessage(LoginRemovedMessage);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            var redirectUrl = Url.Page("./Security", pageHandler: "LinkLoginCallback");
            var properties = SignInManager.ConfigureExternalAuthenticationProperties(
                provider, redirectUrl, GetCurrentUserId());

            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            var userId = await UserManager.GetUserIdAsync(user);
            var info = await SignInManager.GetExternalLoginInfoAsync(userId);
            if (info == null)
                throw new InvalidOperationException(LoadExternalLoginError);

            var result = await UserManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                SetErrorMessage(LoginNotAddedMessage);
                return RedirectToPage();
            }

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            SetSuccessMessage(LoginAddedMessage);
            return RedirectToPage();
        }

        #endregion

        #region Data & Privacy Handlers

        public async Task<IActionResult> OnPostDownloadDataAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            _logger.LogInformation("User with ID '{UserId}' requested their personal data.", GetCurrentUserId());

            var personalData = await CollectPersonalDataAsync(user);

            Response.Headers.TryAdd("Content-Disposition", $"attachment; filename={DownloadFileName}");
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);
            return new FileContentResult(jsonBytes, JsonContentType);
        }

        public async Task<IActionResult> OnPostDeleteAccountAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            OpenPrivacySection = true;
            RequirePassword = await UserManager.HasPasswordAsync(user);

            if (await HasActiveRentalsAsync(user.Id))
            {
                HasActiveRentals = true;
                ModelState.AddModelError(string.Empty,
                    "Cannot delete account while you have active, pending, or accepted rentals. " +
                    "Please complete or cancel all rentals first.");
                await LoadAllSectionDataAsync(user);
                return Page();
            }

            if (!await ValidatePasswordIfRequiredAsync(user))
            {
                await LoadAllSectionDataAsync(user);
                return Page();
            }

            var deleteAllData = DeleteInput?.DeleteAllData ?? false;
            if (deleteAllData)
                await DeleteAllUserDataAsync(user);
            else
                await AnonymizeUserAccountAsync(user);

            return Redirect("~/");
        }

        #endregion

        #region Private Helpers — Data Loading

        /// <summary>Loads all section data for the page GET.</summary>
        private async Task LoadAllSectionDataAsync(ApplicationUser user)
        {
            // Email
            Email = await UserManager.GetEmailAsync(user);
            IsEmailConfirmed = await UserManager.IsEmailConfirmedAsync(user);
            EmailInput = new EmailInputModel { NewEmail = Email };

            // Password
            HasPassword = await UserManager.HasPasswordAsync(user);

            // External logins
            CurrentLogins = await UserManager.GetLoginsAsync(user);
            var allSchemes = await SignInManager.GetExternalAuthenticationSchemesAsync();
            OtherLogins = allSchemes
                .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .ToList();
            ShowRemoveButton = await CanRemoveLoginAsync(user);

            // Delete section
            RequirePassword = HasPassword;
            HasActiveRentals = await HasActiveRentalsAsync(user.Id);
            DeleteInput ??= new DeleteInputModel();
        }

        private async Task<bool> CanRemoveLoginAsync(ApplicationUser user)
        {
            if (_userStore is IUserPasswordStore<ApplicationUser> passwordStore)
            {
                var hash = await passwordStore.GetPasswordHashAsync(user, HttpContext.RequestAborted);
                if (hash != null) return true;
            }
            return CurrentLogins.Count > 1;
        }

        #endregion

        #region Private Helpers — Email

        private async Task SendEmailChangeConfirmationAsync(ApplicationUser user, string newEmail)
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var code = await UserManager.GenerateChangeEmailTokenAsync(user, newEmail);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmailChange",
                pageHandler: null,
                values: new { area = "Identity", userId, email = newEmail, code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                newEmail,
                ConfirmEmailSubject,
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }

        private async Task SendEmailVerificationAsync(ApplicationUser user)
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var email = await UserManager.GetEmailAsync(user);
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                email,
                ConfirmEmailSubject,
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }

        #endregion

        #region Private Helpers — Download Data

        private async Task<Dictionary<string, string>> CollectPersonalDataAsync(ApplicationUser user)
        {
            var data = new Dictionary<string, string>();

            var personalDataProps = typeof(ApplicationUser).GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            foreach (var prop in personalDataProps)
                data.Add(prop.Name, prop.GetValue(user)?.ToString() ?? "null");

            var logins = await UserManager.GetLoginsAsync(user);
            foreach (var login in logins)
                data.Add($"{login.LoginProvider} external login provider key", login.ProviderKey);

            var key = await UserManager.GetAuthenticatorKeyAsync(user);
            data.Add("Authenticator Key", key);

            return data;
        }

        #endregion

        #region Private Helpers — Delete Account

        private async Task<bool> HasActiveRentalsAsync(string userId)
        {
            return await _context.Rentals.AnyAsync(r =>
                (r.RenterId == userId || r.OwnerId == userId) &&
                r.Status != RentalStatus.Completed &&
                r.Status != RentalStatus.Cancelled);
        }

        private async Task<bool> ValidatePasswordIfRequiredAsync(ApplicationUser user)
        {
            if (!RequirePassword) return true;

            if (DeleteInput == null || string.IsNullOrWhiteSpace(DeleteInput.Password))
            {
                ModelState.AddModelError("DeleteInput.Password", "Password is required.");
                return false;
            }

            var valid = await UserManager.CheckPasswordAsync(user, DeleteInput.Password);
            if (!valid)
            {
                ModelState.AddModelError("DeleteInput.Password", IncorrectPasswordError);
                ModelState.AddModelError(string.Empty, IncorrectPasswordError);
            }

            return valid;
        }

        /// <summary>
        /// Anonymizes the account: clears personal info, locks the account,
        /// but preserves items/reviews/rental history on the platform.
        /// </summary>
        private async Task AnonymizeUserAccountAsync(ApplicationUser user)
        {
            var userId = user.Id;

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                await _fileUploadService.DeleteFileAsync(user.ProfilePictureUrl);

            await CleanupStripeCustomerAsync(user.Email);

            user.FirstName = "Deleted account";
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

            var anonymousEmail = $"deleted_{userId[..8]}@deleted.rentmate.local";
            await UserManager.SetEmailAsync(user, anonymousEmail);
            await UserManager.SetUserNameAsync(user, anonymousEmail);

            await UserManager.SetLockoutEnabledAsync(user, true);
            await UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            if (await UserManager.HasPasswordAsync(user))
                await UserManager.RemovePasswordAsync(user);

            await UserManager.UpdateSecurityStampAsync(user);

            var favorites = await _context.AccountItemFavorites
                .Where(f => f.AccountId == userId)
                .ToListAsync();
            _context.AccountItemFavorites.RemoveRange(favorites);
            await _context.SaveChangesAsync();

            await SignInManager.SignOutAsync();
            _logger.LogInformation("User {UserId} anonymized their account.", userId);
        }

        /// <summary>
        /// Full deletion: removes all traces from the platform, then deletes the account.
        /// </summary>
        private async Task DeleteAllUserDataAsync(ApplicationUser user)
        {
            var userId = user.Id;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await CleanupAllUserReferencesAsync(userId, user.Email);

                var result = await UserManager.DeleteAsync(user);
                if (!result.Succeeded)
                    throw new InvalidOperationException(DeleteErrorMessage);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await SignInManager.SignOutAsync();
            _logger.LogInformation("User {UserId} deleted all personal data and account.", userId);
        }

        private async Task CleanupAllUserReferencesAsync(string userId, string userEmail)
        {
            await CleanupCloudinaryImagesAsync(userId);
            await CleanupStripeCustomerAsync(userEmail);

            var reviews = await _context.Reviews.Where(r => r.ReviewerId == userId).ToListAsync();
            _context.Reviews.RemoveRange(reviews);

            var renterRentals = await _context.Rentals.Where(r => r.RenterId == userId).ToListAsync();
            var renterRentalIds = renterRentals.Select(r => r.Id).ToList();
            _context.Rentals.RemoveRange(renterRentals);

            var payments = await _context.Payments.Where(p => p.UserId == userId).ToListAsync();
            foreach (var payment in payments)
                payment.UserId = null;

            var extensions = await _context.RentalExtensions
                .Where(e => e.RequestedByUserId == userId)
                .ToListAsync();
            _context.RentalExtensions.RemoveRange(extensions);

            var evidenceOnOtherRentals = await _context.DisputeEvidences
                .Where(e => e.SubmittedByUserId == userId)
                .Where(e => !renterRentalIds.Contains(e.RentalDeposit.RentalId))
                .ToListAsync();
            _context.DisputeEvidences.RemoveRange(evidenceOnOtherRentals);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up all data references for user {UserId}.", userId);
        }

        private async Task CleanupCloudinaryImagesAsync(string userId)
        {
            var urls = new List<string>();

            var user = await _context.Users.FindAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.ProfilePictureUrl))
                urls.Add(user.ProfilePictureUrl);

            var items = await _context.Items.Include(i => i.Images)
                .Where(i => i.UserId == userId).ToListAsync();

            foreach (var item in items)
            {
                urls.AddRange(item.Images.Select(img => img.ImageUrl));
                if (!string.IsNullOrEmpty(item.ImageUrl) && !urls.Contains(item.ImageUrl))
                    urls.Add(item.ImageUrl);
            }

            var renterEvidence = await _context.DisputeEvidences
                .Where(e => e.RentalDeposit.Rental.RenterId == userId)
                .Select(e => e.Url).ToListAsync();
            urls.AddRange(renterEvidence);

            var itemIds = items.Select(i => i.Id).ToList();
            var ownerEvidence = await _context.DisputeEvidences
                .Where(e => itemIds.Contains(e.RentalDeposit.Rental.ItemId))
                .Select(e => e.Url).ToListAsync();
            urls.AddRange(ownerEvidence);

            var distinct = urls.Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();
            if (distinct.Count > 0)
            {
                await _fileUploadService.DeleteFilesAsync(distinct);
                _logger.LogInformation("Deleted {Count} Cloudinary images for user {UserId}.", distinct.Count, userId);
            }
        }

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
}
