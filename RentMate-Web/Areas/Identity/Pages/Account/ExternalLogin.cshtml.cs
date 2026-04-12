// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for handling external login (OAuth) flows.
    /// </summary>
    [AllowAnonymous]
    public class ExternalLoginModel : BaseIdentityPageModel
    {
        #region Constants

        private const string ExternalProviderErrorKey = "Error from external provider: {0}";
        private const string LoadExternalInfoErrorKey = "Error loading external login information.";
        private const string LoadExternalConfirmErrorKey = "Error loading external login information during confirmation.";
        private const string ConfirmEmailKey = "Confirm your email";
        private const string ConfirmEmailLinkKey = "Please confirm your account by <a href='{0}'>clicking here</a>.";

        #endregion

        #region Dependencies

        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly IStringLocalizer<ExternalLoginModel> _localizer;

        #endregion

        #region Constructor

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender,
            IStringLocalizer<ExternalLoginModel> localizer)
            : base(userManager, signInManager, userStore)
        {
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        [BindProperty]
        public InputModel Input { get; set; }

        public string ProviderDisplayName { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        #endregion

        #region Page Handlers

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = SignInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                ErrorMessage = string.Format(_localizer[ExternalProviderErrorKey], remoteError);
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await SignInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = _localizer[LoadExternalInfoErrorKey];
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            return await HandleExternalSignInAsync(info, returnUrl);
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var info = await SignInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = _localizer[LoadExternalConfirmErrorKey];
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (!ModelState.IsValid)
            {
                ProviderDisplayName = info.ProviderDisplayName;
                ReturnUrl = returnUrl;
                return Page();
            }

            return await CreateExternalUserAsync(info, returnUrl);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Handles the external sign-in result.
        /// </summary>
        private async Task<IActionResult> HandleExternalSignInAsync(ExternalLoginInfo info, string returnUrl)
        {
            var result = await SignInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.",
                    info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }

            // User doesn't have an account - show email confirmation form
            ReturnUrl = returnUrl;
            ProviderDisplayName = info.ProviderDisplayName;

            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                Input = new InputModel { Email = info.Principal.FindFirstValue(ClaimTypes.Email) };
            }

            return Page();
        }

        /// <summary>
        /// Creates a new user from external login and signs them in.
        /// </summary>
        private async Task<IActionResult> CreateExternalUserAsync(ExternalLoginInfo info, string returnUrl)
        {
            var user = CreateUserInstance();

            await UserStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await UserManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                ProviderDisplayName = info.ProviderDisplayName;
                ReturnUrl = returnUrl;
                return Page();
            }

            result = await UserManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                ProviderDisplayName = info.ProviderDisplayName;
                ReturnUrl = returnUrl;
                return Page();
            }

            _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
            await SendEmailConfirmationAsync(user);

            if (UserManager.Options.SignIn.RequireConfirmedAccount)
            {
                return RedirectToPage("./Confirmation", new { type = "register", email = Input.Email });
            }

            await SignInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
            return LocalRedirect(returnUrl);
        }

        /// <summary>
        /// Sends confirmation email to the new user.
        /// </summary>
        private async Task SendEmailConfirmationAsync(ApplicationUser user)
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code },
                protocol: Request.Scheme);

            var emailBody = string.Format(_localizer[ConfirmEmailLinkKey], HtmlEncoder.Default.Encode(callbackUrl));
            await _emailSender.SendEmailAsync(Input.Email, _localizer[ConfirmEmailKey], emailBody);
        }

        #endregion
    }
}