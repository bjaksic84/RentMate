// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for changing user password.
    /// </summary>
    public class ChangePasswordModel : BaseIdentityPageModel
    {
        #region Constants

        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 100;
        private const string PasswordChangedMessage = "Your password has been changed.";

        #endregion

        #region Dependencies

        private readonly ILogger<ChangePasswordModel> _logger;

        #endregion

        #region Constructor

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ChangePasswordModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        #endregion

        #region Properties

        [BindProperty]
        public InputModel Input { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string OldPassword { get; set; }

            [Required]
            [StringLength(MaxPasswordLength, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = MinPasswordLength)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            // Redirect to SetPassword if user has no password (e.g., external login)
            var hasPassword = await UserManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToPage("./SetPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (IsModelStateInvalid())
            {
                return Page();
            }

            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            var changePasswordResult = await UserManager.ChangePasswordAsync(
                user, 
                Input.OldPassword, 
                Input.NewPassword);

            if (!changePasswordResult.Succeeded)
            {
                AddIdentityErrors(changePasswordResult);
                return Page();
            }

            await RefreshSignInAsync(user);
            _logger.LogInformation("User changed their password successfully.");
            SetSuccessMessage(PasswordChangedMessage);

            return RedirectToPage();
        }

        #endregion
    }
}
