// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for setting a password (for users who registered via external login).
    /// </summary>
    public class SetPasswordModel : BaseIdentityPageModel
    {
        #region Constants

        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 100;
        private const string PasswordSetMessage = "Your password has been set.";

        #endregion

        #region Constructor

        public SetPasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
            : base(userManager, signInManager)
        {
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

            // Redirect to ChangePassword if user already has a password
            if (await UserManager.HasPasswordAsync(user))
            {
                return RedirectToPage("./ChangePassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (IsModelStateInvalid()) return Page();

            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            var addPasswordResult = await UserManager.AddPasswordAsync(user, Input.NewPassword);
            if (!addPasswordResult.Succeeded)
            {
                AddIdentityErrors(addPasswordResult);
                return Page();
            }

            await RefreshSignInAsync(user);
            SetSuccessMessage(PasswordSetMessage);

            return RedirectToPage();
        }

        #endregion
    }
}
