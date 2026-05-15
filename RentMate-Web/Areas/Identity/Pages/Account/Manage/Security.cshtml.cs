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
        private const string DownloadFileName = "PersonalData.json";
        private const string JsonContentType = "application/json";

        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 100;

        #endregion

        #region Dependencies

        private readonly IEmailSender _emailSender;
        private readonly ILogger<SecurityModel> _logger;
        private readonly RentMateContext _context;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IAccountLifecycleService _accountLifecycle;

        #endregion

        #region Constructor

        public SecurityModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<SecurityModel> logger,
            RentMateContext context,
            IUserStore<ApplicationUser> userStore,
            IAccountLifecycleService accountLifecycle)
            : base(userManager, signInManager)
        {
            _emailSender = emailSender;
            _logger = logger;
            _context = context;
            _userStore = userStore;
            _accountLifecycle = accountLifecycle;
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
            /// Confirmation phrase required for external-login users who have no password.
            /// Must type "DELETE" to confirm irreversible actions.
            /// </summary>
            public string ConfirmationPhrase { get; set; }
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
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(personalData, personalData.GetType(), jsonOptions);
            return new FileContentResult(jsonBytes, JsonContentType);
        }

        /// <summary>Deactivates the account (reversible). User is redirected to the deactivated page.</summary>
        public async Task<IActionResult> OnPostDeactivateAccountAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            OpenPrivacySection = true;
            RequirePassword = await UserManager.HasPasswordAsync(user);

            if (await _accountLifecycle.HasActiveRentalsAsync(user.Id))
            {
                HasActiveRentals = true;
                ModelState.AddModelError(string.Empty,
                    "Cannot deactivate account while you have active, pending, or accepted rentals. " +
                    "Please complete or cancel all rentals first.");
                await LoadAllSectionDataAsync(user);
                return Page();
            }

            if (!await ValidatePasswordIfRequiredAsync(user))
            {
                await LoadAllSectionDataAsync(user);
                return Page();
            }

            await _accountLifecycle.DeactivateAccountAsync(user.Id, DeactivationSource.User);
            await SignInManager.SignOutAsync();
            return Redirect("~/");
        }

        /// <summary>Permanently deletes (anonymises) the account. Irreversible.</summary>
        public async Task<IActionResult> OnPostDeleteAccountAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            OpenPrivacySection = true;
            RequirePassword = await UserManager.HasPasswordAsync(user);

            if (await _accountLifecycle.HasActiveRentalsAsync(user.Id))
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

            await _accountLifecycle.DeleteAccountAsync(user.Id);
            await SignInManager.SignOutAsync();
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
            HasActiveRentals = await _accountLifecycle.HasActiveRentalsAsync(user.Id);
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
                "/Account/ConfirmEmail",
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

        private async Task<object> CollectPersonalDataAsync(ApplicationUser user)
        {
            var userId = user.Id;

            // --- Profile: fields marked [PersonalData] on ApplicationUser ---
            var profile = typeof(ApplicationUser).GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)))
                .ToDictionary(prop => prop.Name, prop => prop.GetValue(user)?.ToString() ?? "null");

            // Augment profile with non-attribute fields relevant for GDPR export
            profile["FirstName"] = user.FirstName ?? "null";
            profile["LastName"] = user.LastName ?? "null";
            profile["City"] = user.City ?? "null";
            profile["Bio"] = user.Bio ?? "null";
            profile["PreferredLanguage"] = user.PreferredLanguage;
            profile["CreatedAt"] = user.CreatedAt.ToString("O");
            profile["IsDeactivated"] = user.IsDeactivated.ToString();
            profile["PrivacyPolicyVersion"] = user.PrivacyPolicyVersion ?? "null";
            profile["PrivacyPolicyAcceptedAt"] = user.PrivacyPolicyAcceptedAt?.ToString("O") ?? "null";

            // --- External logins ---
            var logins = await UserManager.GetLoginsAsync(user);
            var externalLogins = logins
                .Select(l => new { l.LoginProvider, l.ProviderKey })
                .ToList();

            // --- 2FA ---
            var authenticatorKey = await UserManager.GetAuthenticatorKeyAsync(user);
            profile["AuthenticatorKeySet"] = (authenticatorKey != null).ToString();

            // --- Items listed by this user ---
            var items = await _context.Items
                .Where(i => i.UserId == userId)
                .Select(i => new {
                    i.Id, i.Title, i.Description, i.Price, i.Category,
                    i.Location, i.IsListed, i.IsAdminHidden, i.CreatedAt
                })
                .ToListAsync();

            // --- Rentals where user was the renter ---
            var rentalsAsRenter = await _context.Rentals
                .Where(r => r.RenterId == userId)
                .Select(r => new {
                    r.Id,
                    ItemTitle = r.Item != null ? r.Item.Title : null,
                    r.StartDate, r.EndDate, r.Status, r.TotalPrice, r.CreatedAt
                })
                .ToListAsync();

            // --- Rentals where user was the owner ---
            var rentalsAsOwner = await _context.Rentals
                .Where(r => r.OwnerId == userId)
                .Select(r => new {
                    r.Id,
                    ItemTitle = r.Item != null ? r.Item.Title : null,
                    RenterUsername = r.Renter != null ? r.Renter.UserName : null,
                    r.StartDate, r.EndDate, r.Status, r.TotalPrice, r.CreatedAt
                })
                .ToListAsync();

            // --- Reviews written by this user ---
            var reviews = await _context.Reviews
                .Where(r => r.ReviewerId == userId && !r.IsDeleted)
                .Select(r => new {
                    r.Id,
                    ItemTitle = r.Item != null ? r.Item.Title : null,
                    r.Rating, r.Title, r.Body, r.IsAnonymous, r.CreatedAt
                })
                .ToListAsync();

            // --- Payment records ---
            var payments = await _context.Payments
                .Where(p => p.UserId == userId)
                .Select(p => new {
                    p.Id, p.RentalId, p.Amount, p.Status, p.CreatedAt
                })
                .ToListAsync();

            // --- Favorited items ---
            var favorites = await _context.AccountItemFavorites
                .Where(f => f.AccountId == userId)
                .Select(f => new {
                    f.ItemId,
                    ItemTitle = f.Item != null ? f.Item.Title : null,
                    f.CreatedAt
                })
                .ToListAsync();

            // --- Deposit/dispute records (rentals where user was renter) ---
            var deposits = await _context.RentalDeposits
                .Where(d => d.Rental.RenterId == userId)
                .Select(d => new {
                    d.Id, d.RentalId, d.Amount, d.Status,
                    d.ChargedAmount, d.ChargeReason,
                    d.DisputeReason, d.AuthorizedAt,
                    d.ReleasedAt, d.ChargedAt, d.DisputedAt, d.CreatedAt
                })
                .ToListAsync();

            // --- Extension requests made by this user ---
            var extensions = await _context.RentalExtensions
                .Where(e => e.RequestedByUserId == userId)
                .Select(e => new {
                    e.Id, e.RentalId, e.OriginalEndDate, e.NewEndDate,
                    e.AdditionalCost, e.Status, e.CreatedAt
                })
                .ToListAsync();

            // --- Cookie consent records ---
            var cookieConsents = await _context.CookieConsents
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.ConsentedAt)
                .Select(c => new {
                    c.NecessaryCookies, c.AnalyticsCookies,
                    c.MarketingCookies, c.ConsentedAt
                })
                .ToListAsync();

            return new
            {
                Profile = profile,
                ExternalLogins = externalLogins,
                Items = items,
                RentalsAsRenter = rentalsAsRenter,
                RentalsAsOwner = rentalsAsOwner,
                Reviews = reviews,
                Payments = payments,
                Favorites = favorites,
                Deposits = deposits,
                Extensions = extensions,
                CookieConsent = cookieConsents
            };
        }

        #endregion

        #region Private Helpers — Delete Account

        private const string RequiredConfirmationPhrase = "DELETE";

        private async Task<bool> ValidatePasswordIfRequiredAsync(ApplicationUser user)
        {
            if (!RequirePassword)
            {
                // External-login users must type a confirmation phrase instead
                if (DeleteInput == null ||
                    !string.Equals(DeleteInput.ConfirmationPhrase?.Trim(), RequiredConfirmationPhrase, StringComparison.Ordinal))
                {
                    ModelState.AddModelError("DeleteInput.ConfirmationPhrase",
                        $"Please type \"{RequiredConfirmationPhrase}\" to confirm.");
                    return false;
                }
                return true;
            }

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

        #endregion
    }
}
