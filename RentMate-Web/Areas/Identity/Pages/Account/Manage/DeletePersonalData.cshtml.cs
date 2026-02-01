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
    /// Page model for deleting user's personal data and account.
    /// </summary>
    public class DeletePersonalDataModel : BaseIdentityPageModel
    {
        #region Constants

        private const string IncorrectPasswordError = "Incorrect password.";
        private const string DeleteErrorMessage = "Unexpected error occurred deleting user.";

        #endregion

        #region Dependencies

        private readonly ILogger<DeletePersonalDataModel> _logger;

        #endregion

        #region Constructor

        public DeletePersonalDataModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<DeletePersonalDataModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        #endregion

        #region Properties

        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>Whether password confirmation is required for deletion.</summary>
        public bool RequirePassword { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGet()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            RequirePassword = await UserManager.HasPasswordAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            RequirePassword = await UserManager.HasPasswordAsync(user);
            
            if (!await ValidatePasswordIfRequiredAsync(user))
            {
                return Page();
            }

            await DeleteUserAccountAsync(user);
            return Redirect("~/");
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Validates password if the user has one set.
        /// </summary>
        private async Task<bool> ValidatePasswordIfRequiredAsync(ApplicationUser user)
        {
            if (!RequirePassword) return true;

            var isPasswordValid = await UserManager.CheckPasswordAsync(user, Input.Password);
            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, IncorrectPasswordError);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Deletes the user account and signs them out.
        /// </summary>
        private async Task DeleteUserAccountAsync(ApplicationUser user)
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var result = await UserManager.DeleteAsync(user);
            
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(DeleteErrorMessage);
            }

            await SignInManager.SignOutAsync();
            _logger.LogInformation("User with ID '{UserId}' deleted themselves.", userId);
        }

        #endregion
    }
}
