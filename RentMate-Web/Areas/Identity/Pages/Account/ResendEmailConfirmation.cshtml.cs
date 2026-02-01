// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for resending email confirmation.
    /// </summary>
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        #region Constants

        private const string ConfirmEmailKey = "Confirm your email";
        private const string PleaseConfirmKey = "Please confirm your account by";
        private const string ClickingHereKey = "clicking here";
        private const string VerificationSentKey = "Verification email sent. Please check your email.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<ResendEmailConfirmationModel> _localizer;

        #endregion

        #region Constructor

        public ResendEmailConfirmationModel(
            UserManager<ApplicationUser> userManager, 
            IEmailSender emailSender,
            IStringLocalizer<ResendEmailConfirmationModel> localizer)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        [BindProperty]
        public InputModel Input { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required(ErrorMessage = "The {0} field is required.")]
            [EmailAddress(ErrorMessage = "The {0} field is not a valid e-mail address.")]
            [Display(Name = "Email")]
            public string Email { get; set; }
        }

        #endregion

        #region Page Handlers

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                ShowVerificationSentMessage();
                return Page();
            }

            await SendConfirmationEmailAsync(user);
            ShowVerificationSentMessage();
            return Page();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Sends the confirmation email to the user.
        /// </summary>
        private async Task SendConfirmationEmailAsync(ApplicationUser user)
        {
            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { userId, code },
                protocol: Request.Scheme);

            var emailBody = $"{_localizer[PleaseConfirmKey]} <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>{_localizer[ClickingHereKey]}</a>.";
            await _emailSender.SendEmailAsync(Input.Email, _localizer[ConfirmEmailKey], emailBody);
        }

        /// <summary>
        /// Shows the verification sent message.
        /// </summary>
        private void ShowVerificationSentMessage()
        {
            ModelState.AddModelError(string.Empty, _localizer[VerificationSentKey]);
        }

        #endregion
    }
}