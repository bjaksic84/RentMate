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
    /// Page model for resetting the authenticator app key.
    /// </summary>
    public class ResetAuthenticatorModel : BaseIdentityPageModel
    {
        #region Constants

        private const string AuthenticatorResetMessage = "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key.";

        #endregion

        #region Dependencies

        private readonly ILogger<ResetAuthenticatorModel> _logger;

        #endregion

        #region Constructor

        public ResetAuthenticatorModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ResetAuthenticatorModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGet()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            return errorResult ?? Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await ResetAuthenticatorAsync(user);
            return RedirectToPage("./EnableAuthenticator");
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Resets the authenticator key and refreshes sign-in.
        /// </summary>
        private async Task ResetAuthenticatorAsync(ApplicationUser user)
        {
            await UserManager.SetTwoFactorEnabledAsync(user, false);
            await UserManager.ResetAuthenticatorKeyAsync(user);
            
            _logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", user.Id);

            await RefreshSignInAsync(user);
            SetSuccessMessage(AuthenticatorResetMessage);
        }

        #endregion
    }
}
