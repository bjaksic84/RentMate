// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for confirming email change.
    /// </summary>
    public class ConfirmEmailChangeModel : PageModel
    {
        #region Constants

        private const string UserNotFoundKey = "Unable to load user with ID '{0}'.";
        private const string EmailChangeErrorKey = "Error changing email.";
        private const string UsernameChangeErrorKey = "Error changing user name.";
        private const string EmailChangeSuccessKey = "Thank you for confirming your email change.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IStringLocalizer<ConfirmEmailChangeModel> _localizer;

        #endregion

        #region Constructor

        public ConfirmEmailChangeModel(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager,
            IStringLocalizer<ConfirmEmailChangeModel> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        [TempData]
        public string StatusMessage { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync(string userId, string email, string code)
        {
            if (userId == null || email == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(string.Format(_localizer[UserNotFoundKey], userId));
            }

            return await ChangeEmailAndUsernameAsync(user, email, code);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Changes the user's email and username.
        /// </summary>
        private async Task<IActionResult> ChangeEmailAndUsernameAsync(ApplicationUser user, string email, string code)
        {
            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ChangeEmailAsync(user, email, decodedCode);
            
            if (!result.Succeeded)
            {
                StatusMessage = _localizer[EmailChangeErrorKey];
                return Page();
            }

            // Email and username are the same in this app
            var setUserNameResult = await _userManager.SetUserNameAsync(user, email);
            if (!setUserNameResult.Succeeded)
            {
                StatusMessage = _localizer[UsernameChangeErrorKey];
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = _localizer[EmailChangeSuccessKey];
            return Page();
        }

        #endregion
    }
}