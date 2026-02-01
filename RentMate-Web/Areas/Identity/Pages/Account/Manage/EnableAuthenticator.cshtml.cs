// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for enabling authenticator app-based 2FA.
    /// </summary>
    public class EnableAuthenticatorModel : BaseIdentityPageModel
    {
        #region Constants

        private const int MinCodeLength = 6;
        private const int MaxCodeLength = 7;
        private const int KeyGroupSize = 4;
        private const int RecoveryCodeCount = 10;
        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
        private const string AuthenticatorIssuer = "Microsoft.AspNetCore.Identity.UI";
        private const string InvalidCodeError = "Verification code is invalid.";
        private const string AuthenticatorVerifiedMessage = "Your authenticator app has been verified.";

        #endregion

        #region Dependencies

        private readonly ILogger<EnableAuthenticatorModel> _logger;
        private readonly UrlEncoder _urlEncoder;

        #endregion

        #region Constructor

        public EnableAuthenticatorModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<EnableAuthenticatorModel> logger,
            UrlEncoder urlEncoder)
            : base(userManager, signInManager)
        {
            _logger = logger;
            _urlEncoder = urlEncoder;
        }

        #endregion

        #region Properties

        /// <summary>Formatted shared key for manual entry.</summary>
        public string SharedKey { get; set; }

        /// <summary>URI for QR code generation.</summary>
        public string AuthenticatorUri { get; set; }

        /// <summary>Generated recovery codes (passed via TempData).</summary>
        [TempData]
        public string[] RecoveryCodes { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required]
            [StringLength(MaxCodeLength, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = MinCodeLength)]
            [DataType(DataType.Text)]
            [Display(Name = "Verification Code")]
            public string Code { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            if (IsModelStateInvalid())
            {
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            if (!await ValidateVerificationCodeAsync(user))
            {
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            return await EnableTwoFactorAndRedirectAsync(user);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Loads the authenticator key and generates QR code URI.
        /// </summary>
        private async Task LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
        {
            var unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await UserManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
            }

            SharedKey = FormatKey(unformattedKey);

            var email = await UserManager.GetEmailAsync(user);
            AuthenticatorUri = GenerateQrCodeUri(email, unformattedKey);
        }

        /// <summary>
        /// Validates the user-entered verification code.
        /// </summary>
        private async Task<bool> ValidateVerificationCodeAsync(ApplicationUser user)
        {
            var verificationCode = NormalizeVerificationCode(Input.Code);
            var tokenProvider = UserManager.Options.Tokens.AuthenticatorTokenProvider;
            
            var isValid = await UserManager.VerifyTwoFactorTokenAsync(user, tokenProvider, verificationCode);
            if (!isValid)
            {
                ModelState.AddModelError("Input.Code", InvalidCodeError);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Enables 2FA and redirects appropriately based on recovery code status.
        /// </summary>
        private async Task<IActionResult> EnableTwoFactorAndRedirectAsync(ApplicationUser user)
        {
            await UserManager.SetTwoFactorEnabledAsync(user, true);
            var userId = await UserManager.GetUserIdAsync(user);
            _logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", userId);

            SetSuccessMessage(AuthenticatorVerifiedMessage);

            if (await UserManager.CountRecoveryCodesAsync(user) == 0)
            {
                var recoveryCodes = await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
                RecoveryCodes = recoveryCodes.ToArray();
                return RedirectToPage("./ShowRecoveryCodes");
            }

            return RedirectToPage("./TwoFactorAuthentication");
        }

        /// <summary>
        /// Removes spaces and hyphens from verification code.
        /// </summary>
        private static string NormalizeVerificationCode(string code)
        {
            return code.Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        /// <summary>
        /// Formats the authenticator key for display (groups of 4 characters).
        /// </summary>
        private static string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            int currentPosition = 0;
            
            while (currentPosition + KeyGroupSize < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, KeyGroupSize)).Append(' ');
                currentPosition += KeyGroupSize;
            }
            
            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Generates the URI for QR code generation.
        /// </summary>
        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                AuthenticatorUriFormat,
                _urlEncoder.Encode(AuthenticatorIssuer),
                _urlEncoder.Encode(email),
                unformattedKey);
        }

        #endregion
    }
}
