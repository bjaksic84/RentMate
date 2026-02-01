// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for managing two-factor authentication settings.
    /// </summary>
    public class TwoFactorAuthenticationModel : BaseIdentityPageModel
    {
        #region Constants

        private const string BrowserForgottenMessage = "The current browser has been forgotten. When you login again from this browser you will be prompted for your 2fa code.";

        #endregion

        #region Dependencies

        private readonly ILogger<TwoFactorAuthenticationModel> _logger;

        #endregion

        #region Constructor

        public TwoFactorAuthenticationModel(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, 
            ILogger<TwoFactorAuthenticationModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        #endregion

        #region Properties

        /// <summary>Whether user has set up an authenticator app.</summary>
        public bool HasAuthenticator { get; set; }

        /// <summary>Number of remaining recovery codes.</summary>
        public int RecoveryCodesLeft { get; set; }

        /// <summary>Whether 2FA is currently enabled.</summary>
        [BindProperty]
        public bool Is2faEnabled { get; set; }

        /// <summary>Whether this browser is remembered for 2FA.</summary>
        public bool IsMachineRemembered { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await Load2faStatusAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await SignInManager.ForgetTwoFactorClientAsync();
            SetSuccessMessage(BrowserForgottenMessage);
            return RedirectToPage();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Loads all 2FA status information for display.
        /// </summary>
        private async Task Load2faStatusAsync(ApplicationUser user)
        {
            HasAuthenticator = await UserManager.GetAuthenticatorKeyAsync(user) != null;
            Is2faEnabled = await UserManager.GetTwoFactorEnabledAsync(user);
            IsMachineRemembered = await SignInManager.IsTwoFactorClientRememberedAsync(user);
            RecoveryCodesLeft = await UserManager.CountRecoveryCodesAsync(user);
        }

        #endregion
    }
}
