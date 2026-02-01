// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
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
    /// Page model for email confirmation.
    /// </summary>
    public class ConfirmEmailModel : PageModel
    {
        #region Constants

        private const string UserNotFoundKey = "Unable to load user with ID '{0}'.";
        private const string EmailConfirmedKey = "Thank you for confirming your email.";
        private const string EmailConfirmErrorKey = "Error confirming your email.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<ConfirmEmailModel> _localizer;

        #endregion

        #region Constructor

        public ConfirmEmailModel(
            UserManager<ApplicationUser> userManager, 
            IStringLocalizer<ConfirmEmailModel> localizer)
        {
            _userManager = userManager;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        [TempData]
        public string? StatusMessage { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(string.Format(_localizer[UserNotFoundKey], userId));
            }

            await ConfirmUserEmailAsync(user, code);
            return Page();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Confirms the user's email and sets the appropriate status message.
        /// </summary>
        private async Task ConfirmUserEmailAsync(ApplicationUser user, string code)
        {
            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
            
            StatusMessage = result.Succeeded 
                ? _localizer[EmailConfirmedKey] 
                : _localizer[EmailConfirmErrorKey];
        }

        #endregion
    }
}