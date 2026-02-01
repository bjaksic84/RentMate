// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for registration confirmation.
    /// </summary>
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        #region Constants

        private const string UserNotFoundKey = "Unable to load user with email '{0}'.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _sender;
        private readonly IStringLocalizer<RegisterConfirmationModel> _localizer;

        #endregion

        #region Constructor

        public RegisterConfirmationModel(
            UserManager<ApplicationUser> userManager, 
            IEmailSender sender,
            IStringLocalizer<RegisterConfirmationModel> localizer)
        {
            _userManager = userManager;
            _sender = sender;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        public string Email { get; set; }

        /// <summary>Whether to show the direct confirmation link (for testing).</summary>
        public bool DisplayConfirmAccountLink { get; set; }

        public string EmailConfirmationUrl { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync(string email, string returnUrl = null)
        {
            if (email == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound(string.Format(_localizer[UserNotFoundKey], email));
            }

            Email = email;
            await GenerateConfirmationLinkIfNeededAsync(user, returnUrl);

            return Page();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Generates confirmation link for display (used during development/testing).
        /// </summary>
        private async Task GenerateConfirmationLinkIfNeededAsync(ApplicationUser user, string returnUrl)
        {
            // TODO: Remove this code when using a real email sender
            DisplayConfirmAccountLink = true;
            
            if (!DisplayConfirmAccountLink) return;

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            EmailConfirmationUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code, returnUrl = returnUrl ?? Url.Content("~/") },
                protocol: Request.Scheme);
        }

        #endregion
    }
}