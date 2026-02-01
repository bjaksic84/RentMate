// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for two-factor authentication login.
    /// </summary>
    public class LoginWith2faModel : PageModel
    {
        #region Constants

        private const int MinCodeLength = 6;
        private const int MaxCodeLength = 7;
        private const string UnableToLoad2faUserKey = "Unable to load two-factor authentication user.";
        private const string InvalidAuthenticatorCodeKey = "Invalid authenticator code.";

        #endregion

        #region Dependencies

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginWith2faModel> _logger;
        private readonly IStringLocalizer<LoginWith2faModel> _localizer;

        #endregion

        #region Constructor

        public LoginWith2faModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginWith2faModel> logger,
            IStringLocalizer<LoginWith2faModel> localizer)
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

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required(ErrorMessage = "The {0} field is required.")]
            [StringLength(MaxCodeLength, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = MinCodeLength)]
            [DataType(DataType.Text)]
            [Display(Name = "Authenticator code")]
            public string TwoFactorCode { get; set; }

            [Display(Name = "Remember this machine")]
            public bool RememberMachine { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string returnUrl = null)
        {
            await EnsureTwoFactorUserLoadedAsync();

            ReturnUrl = returnUrl;
            RememberMe = rememberMe;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string returnUrl = null)
        {
            if (!ModelState.IsValid) return Page();

            returnUrl ??= Url.Content("~/");

            var user = await EnsureTwoFactorUserLoadedAsync();
            return await AttemptTwoFactorSignInAsync(user, rememberMe, returnUrl);
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
        /// Attempts to sign in the user with 2FA code.
        /// </summary>
        private async Task<IActionResult> AttemptTwoFactorSignInAsync(
            ApplicationUser user, bool rememberMe, string returnUrl)
        {
            var authenticatorCode = NormalizeAuthenticatorCode(Input.TwoFactorCode);
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
                authenticatorCode, rememberMe, Input.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID '{UserId}' logged in with 2fa.", user.Id);
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID '{UserId}' account locked out.", user.Id);
                return RedirectToPage("./Lockout");
            }

            _logger.LogWarning("Invalid authenticator code entered for user with ID '{UserId}'.", user.Id);
            ModelState.AddModelError(string.Empty, _localizer[InvalidAuthenticatorCodeKey]);
            return Page();
        }

        /// <summary>
        /// Normalizes the authenticator code by removing spaces and hyphens.
        /// </summary>
        private static string NormalizeAuthenticatorCode(string code)
        {
            return code.Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        #endregion
    }
}