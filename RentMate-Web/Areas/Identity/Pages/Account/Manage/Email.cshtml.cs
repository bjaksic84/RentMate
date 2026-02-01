// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for managing user email address.
    /// </summary>
    public class EmailModel : BaseIdentityPageModel
    {
        #region Constants

        private const string EmailUnchangedMessage = "Your email is unchanged.";
        private const string EmailChangeConfirmationSentMessage = "Confirmation link to change email sent. Please check your email.";
        private const string VerificationEmailSentMessage = "Verification email sent. Please check your email.";
        private const string ConfirmEmailSubject = "Confirm your email";

        #endregion

        #region Dependencies

        private readonly IEmailSender _emailSender;

        #endregion

        #region Constructor

        public EmailModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender)
            : base(userManager, signInManager)
        {
            _emailSender = emailSender;
        }

        #endregion

        #region Properties

        /// <summary>Current email address.</summary>
        public string Email { get; set; }

        /// <summary>Whether the email is confirmed.</summary>
        public bool IsEmailConfirmed { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "New email")]
            public string NewEmail { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await LoadEmailDataAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostChangeEmailAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            if (IsModelStateInvalid())
            {
                await LoadEmailDataAsync(user);
                return Page();
            }

            var currentEmail = await UserManager.GetEmailAsync(user);
            if (Input.NewEmail == currentEmail)
            {
                SetSuccessMessage(EmailUnchangedMessage);
                return RedirectToPage();
            }

            await SendEmailChangeConfirmationAsync(user, Input.NewEmail);
            SetSuccessMessage(EmailChangeConfirmationSentMessage);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            if (IsModelStateInvalid())
            {
                await LoadEmailDataAsync(user);
                return Page();
            }

            await SendEmailVerificationAsync(user);
            SetSuccessMessage(VerificationEmailSentMessage);
            return RedirectToPage();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Loads email-related data for the current user.
        /// </summary>
        private async Task LoadEmailDataAsync(ApplicationUser user)
        {
            var email = await UserManager.GetEmailAsync(user);
            Email = email;

            Input = new InputModel
            {
                NewEmail = email,
            };

            IsEmailConfirmed = await UserManager.IsEmailConfirmedAsync(user);
        }

        /// <summary>
        /// Sends email change confirmation to the new email address.
        /// </summary>
        private async Task SendEmailChangeConfirmationAsync(ApplicationUser user, string newEmail)
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var code = await UserManager.GenerateChangeEmailTokenAsync(user, newEmail);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmailChange",
                pageHandler: null,
                values: new { area = "Identity", userId = userId, email = newEmail, code = code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                newEmail,
                ConfirmEmailSubject,
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }

        /// <summary>
        /// Sends email verification to the current email address.
        /// </summary>
        private async Task SendEmailVerificationAsync(ApplicationUser user)
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var email = await UserManager.GetEmailAsync(user);
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = userId, code = code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                email,
                ConfirmEmailSubject,
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }

        #endregion
    }
}
