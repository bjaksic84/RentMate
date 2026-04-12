// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for resetting user password.
    /// </summary>
    public class ResetPasswordModel : BaseIdentityPageModel
    {
        #region Constants

        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 100;
        private const string CodeRequiredErrorKey = "A code must be supplied for password reset.";

        #endregion

        #region Dependencies

        private readonly IStringLocalizer<ResetPasswordModel> _localizer;

        #endregion

        #region Constructor

        public ResetPasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IStringLocalizer<ResetPasswordModel> localizer)
            : base(userManager, signInManager)
        {
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

            [Required(ErrorMessage = "The {0} field is required.")]
            [StringLength(MaxPasswordLength, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = MinPasswordLength)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        #endregion

        #region Page Handlers

        public IActionResult OnGet(string code = null)
        {
            if (code == null)
            {
                return BadRequest(_localizer[CodeRequiredErrorKey]);
            }

            Input = new InputModel
            {
                Code = DecodeResetCode(code)
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await UserManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToPage("./Confirmation", new { type = "password-reset" });
            }

            return await ResetUserPasswordAsync(user);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Decodes the Base64Url reset code.
        /// </summary>
        private static string DecodeResetCode(string code)
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }

        /// <summary>
        /// Attempts to reset the user's password.
        /// </summary>
        private async Task<IActionResult> ResetUserPasswordAsync(ApplicationUser user)
        {
            var result = await UserManager.ResetPasswordAsync(user, Input.Code, Input.Password);

            if (result.Succeeded)
            {
                return RedirectToPage("./Confirmation", new { type = "password-reset" });
            }

            AddIdentityErrors(result);
            return Page();
        }

        #endregion
    }
}