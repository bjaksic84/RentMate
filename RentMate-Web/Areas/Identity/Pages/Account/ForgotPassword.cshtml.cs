// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
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
    /// Page model for the forgot password flow.
    /// </summary>
    public class ForgotPasswordModel : PageModel
    {
        #region Constants

        private const string ResetPasswordKey = "Reset Password";
        private const string ResetPasswordLinkKey = "Please reset your password by <a href='{0}'>clicking here</a>.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<ForgotPasswordModel> _localizer;

        #endregion

        #region Constructor

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager, 
            IEmailSender emailSender,
            IStringLocalizer<ForgotPasswordModel> localizer)
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
            [Required(ErrorMessage = "The Email field is required.")]
            [EmailAddress(ErrorMessage = "The Email field is not a valid e-mail address.")]
            [Display(Name = "Email")]
            public string Email { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (!await IsValidUserForPasswordResetAsync(user))
            {
                // Don't reveal that the user does not exist or is not confirmed
                return RedirectToPage("./Confirmation", new { type = "password-reset-sent" });
            }

            await SendPasswordResetEmailAsync(user);
            return RedirectToPage("./Confirmation", new { type = "password-reset-sent" });
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Checks if the user exists and has a confirmed email.
        /// </summary>
        private async Task<bool> IsValidUserForPasswordResetAsync(ApplicationUser user)
        {
            return user != null && await _userManager.IsEmailConfirmedAsync(user);
        }

        /// <summary>
        /// Sends the password reset email to the user.
        /// </summary>
        private async Task SendPasswordResetEmailAsync(ApplicationUser user)
        {
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            var emailBody = string.Format(_localizer[ResetPasswordLinkKey], HtmlEncoder.Default.Encode(callbackUrl));
            await _emailSender.SendEmailAsync(Input.Email, _localizer[ResetPasswordKey], emailBody);
        }

        #endregion
    }
}