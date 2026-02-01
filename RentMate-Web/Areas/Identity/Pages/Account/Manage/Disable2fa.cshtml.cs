// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for disabling two-factor authentication.
    /// </summary>
    public class Disable2faModel : BaseIdentityPageModel
    {
        #region Constants

        private const string TwoFactorNotEnabledError = "Cannot disable 2FA for user as it's not currently enabled.";
        private const string DisableErrorMessage = "Unexpected error occurred disabling 2FA.";
        private const string TwoFactorDisabledMessage = "2fa has been disabled. You can reenable 2fa when you setup an authenticator app";

        #endregion

        #region Dependencies

        private readonly ILogger<Disable2faModel> _logger;

        #endregion

        #region Constructor

        public Disable2faModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<Disable2faModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGet()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await EnsureTwoFactorIsEnabledAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            var disable2faResult = await UserManager.SetTwoFactorEnabledAsync(user, false);
            if (!disable2faResult.Succeeded)
            {
                throw new InvalidOperationException(DisableErrorMessage);
            }

            _logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", GetCurrentUserId());
            SetSuccessMessage(TwoFactorDisabledMessage);
            return RedirectToPage("./TwoFactorAuthentication");
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Ensures 2FA is enabled before allowing disable action.
        /// </summary>
        private async Task EnsureTwoFactorIsEnabledAsync(ApplicationUser user)
        {
            if (!await UserManager.GetTwoFactorEnabledAsync(user))
            {
                throw new InvalidOperationException(TwoFactorNotEnabledError);
            }
        }

        #endregion
    }
}
