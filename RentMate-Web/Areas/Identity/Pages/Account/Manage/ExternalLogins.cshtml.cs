// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for managing external login providers (OAuth).
    /// </summary>
    public class ExternalLoginsModel : BaseIdentityPageModel
    {
        #region Constants

        private const string LoginRemovedMessage = "The external login was removed.";
        private const string LoginNotRemovedMessage = "The external login was not removed.";
        private const string LoginAddedMessage = "The external login was added.";
        private const string LoginNotAddedMessage = "The external login was not added. External logins can only be associated with one account.";
        private const string LoadExternalLoginError = "Unexpected error occurred loading external login info.";

        #endregion

        #region Dependencies

        private readonly IUserStore<ApplicationUser> _userStore;

        #endregion

        #region Constructor

        public ExternalLoginsModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserStore<ApplicationUser> userStore)
            : base(userManager, signInManager)
        {
            _userStore = userStore;
        }

        #endregion

        #region Properties

        /// <summary>List of currently linked external logins.</summary>
        public IList<UserLoginInfo> CurrentLogins { get; set; }

        /// <summary>List of available external login providers not yet linked.</summary>
        public IList<AuthenticationScheme> OtherLogins { get; set; }

        /// <summary>Whether the remove button should be shown.</summary>
        public bool ShowRemoveButton { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await LoadExternalLoginsAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            var result = await UserManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                SetSuccessMessage(LoginNotRemovedMessage);
                return RedirectToPage();
            }

            await RefreshSignInAsync(user);
            SetSuccessMessage(LoginRemovedMessage);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // Request a redirect to the external login provider
            var redirectUrl = Url.Page("./ExternalLogins", pageHandler: "LinkLoginCallback");
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
            {
                throw new InvalidOperationException(LoadExternalLoginError);
            }

            var result = await UserManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                SetSuccessMessage(LoginNotAddedMessage);
                return RedirectToPage();
            }

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            SetSuccessMessage(LoginAddedMessage);
            return RedirectToPage();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Loads current and available external logins for the user.
        /// </summary>
        private async Task LoadExternalLoginsAsync(ApplicationUser user)
        {
            CurrentLogins = await UserManager.GetLoginsAsync(user);
            
            var allSchemes = await SignInManager.GetExternalAuthenticationSchemesAsync();
            OtherLogins = allSchemes
                .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .ToList();

            ShowRemoveButton = await CanRemoveLoginAsync(user);
        }

        /// <summary>
        /// Determines if user can remove a login (must have password or more than one login).
        /// </summary>
        private async Task<bool> CanRemoveLoginAsync(ApplicationUser user)
        {
            if (_userStore is IUserPasswordStore<ApplicationUser> userPasswordStore)
            {
                var passwordHash = await userPasswordStore.GetPasswordHashAsync(user, HttpContext.RequestAborted);
                if (passwordHash != null) return true;
            }

            return CurrentLogins.Count > 1;
        }

        #endregion
    }
}
