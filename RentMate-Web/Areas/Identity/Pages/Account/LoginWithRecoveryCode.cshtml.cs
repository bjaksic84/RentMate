// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for login with 2FA recovery code.
    /// </summary>
    public class LoginWithRecoveryCodeModel : PageModel
    {
        #region Constants

        private const string UnableToLoad2faUserKey = "Unable to load two-factor authentication user.";
        private const string InvalidRecoveryCodeKey = "Invalid recovery code entered.";

        #endregion

        #region Dependencies

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginWithRecoveryCodeModel> _logger;
        private readonly IStringLocalizer<LoginWithRecoveryCodeModel> _localizer;

        #endregion

        #region Constructor

        public LoginWithRecoveryCodeModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginWithRecoveryCodeModel> logger,
            IStringLocalizer<LoginWithRecoveryCodeModel> localizer)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required(ErrorMessage = "The {0} field is required.")]
            [DataType(DataType.Text)]
            [Display(Name = "Recovery Code")]
            public string RecoveryCode { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            await EnsureTwoFactorUserLoadedAsync();
            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            if (!ModelState.IsValid) return Page();

            var user = await EnsureTwoFactorUserLoadedAsync();
            return await AttemptRecoveryCodeSignInAsync(user, returnUrl);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Ensures the 2FA user can be loaded.
        /// </summary>
        private async Task<ApplicationUser> EnsureTwoFactorUserLoadedAsync()
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new InvalidOperationException(_localizer[UnableToLoad2faUserKey]);
            }
            return user;
        }

        /// <summary>
        /// Attempts to sign in with recovery code.
        /// </summary>
        private async Task<IActionResult> AttemptRecoveryCodeSignInAsync(ApplicationUser user, string returnUrl)
        {
            var recoveryCode = Input.RecoveryCode.Replace(" ", string.Empty);
            var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID '{UserId}' logged in with a recovery code.", user.Id);
                return LocalRedirect(returnUrl ?? Url.Content("~/"));
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }

            _logger.LogWarning("Invalid recovery code entered for user with ID '{UserId}' ", user.Id);
            ModelState.AddModelError(string.Empty, _localizer[InvalidRecoveryCodeKey]);
            return Page();
        }

        #endregion
    }
}