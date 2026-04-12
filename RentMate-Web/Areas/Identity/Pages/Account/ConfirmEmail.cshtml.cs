// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Handles both initial email confirmation and email change confirmation.
    /// When <c>email</c> query parameter is present, this is an email change; otherwise initial confirmation.
    /// </summary>
    public class ConfirmEmailModel : BaseIdentityPageModel
    {
        #region Constants

        private const string UserNotFoundKey = "Unable to load user with ID '{0}'.";
        private const string EmailConfirmedKey = "Thank you for confirming your email.";
        private const string EmailConfirmErrorKey = "Error confirming your email.";
        private const string EmailChangeSuccessKey = "Thank you for confirming your email change.";
        private const string EmailChangeErrorKey = "Error changing email.";

        #endregion

        #region Dependencies

        private readonly IStringLocalizer<ConfirmEmailModel> _localizer;

        #endregion

        #region Constructor

        public ConfirmEmailModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IStringLocalizer<ConfirmEmailModel> localizer)
            : base(userManager, signInManager)
        {
            _localizer = localizer;
        }

        #endregion

        #region Properties

        /// <summary>Whether this was an email change confirmation (vs initial email confirm).</summary>
        public bool IsEmailChange { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync(string userId, string code, string? email = null)
        {
            if (userId == null || code == null)
                return RedirectToPage("/Index");

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(string.Format(_localizer[UserNotFoundKey], userId));

            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

            if (email != null)
                await ConfirmEmailChangeAsync(user, email, decodedCode);
            else
                await ConfirmUserEmailAsync(user, decodedCode);

            return Page();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Confirms the user's email and sets the appropriate status message.
        /// </summary>
        private async Task ConfirmUserEmailAsync(ApplicationUser user, string decodedCode)
        {
            var result = await UserManager.ConfirmEmailAsync(user, decodedCode);

            StatusMessage = result.Succeeded
                ? _localizer[EmailConfirmedKey]
                : _localizer[EmailConfirmErrorKey];
        }

        /// <summary>
        /// Confirms the email change token and updates the user's email.
        /// Username is intentionally NOT synced — it is independently managed by the user.
        /// </summary>
        private async Task ConfirmEmailChangeAsync(ApplicationUser user, string email, string decodedCode)
        {
            IsEmailChange = true;
            var result = await UserManager.ChangeEmailAsync(user, email, decodedCode);

            if (!result.Succeeded)
            {
                StatusMessage = _localizer[EmailChangeErrorKey];
                return;
            }

            await SignInManager.RefreshSignInAsync(user);
            StatusMessage = _localizer[EmailChangeSuccessKey];
        }

        #endregion
    }
}
