// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using RentMate.Infrastructure.Validation;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for new user registration.
    /// </summary>
    public class RegisterModel : BaseIdentityPageModel
    {
        #region Constants

        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 100;
        private const string DefaultUserRole = "User";
        private const string ConfirmEmailKey = "Confirm your email";
        private const string PleaseConfirmKey = "Please confirm your account by";
        private const string ClickingHereKey = "clicking here";
        private const string CurrentPrivacyPolicyVersion = "1.0";

        #endregion

        #region Dependencies

        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<RegisterModel> _localizer;

        #endregion

        #region Constructor

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IStringLocalizer<RegisterModel> localizer)
            : base(userManager, signInManager, userStore)
        {
            _roleManager = roleManager;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

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

            [MustBeTrue(ErrorMessage = "You must agree to the Privacy Policy and Terms of Service to register.")]
            [Display(Name = "Privacy Policy consent")]
            public bool ConsentToPrivacyPolicy { get; set; }
        }

        #endregion

        #region Page Handlers

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid) return Page();

            return await CreateUserAndSignInAsync(returnUrl);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Creates a new user, assigns role, sends confirmation email, and signs in.
        /// </summary>
        private async Task<IActionResult> CreateUserAndSignInAsync(string returnUrl)
        {
            var user = CreateUserInstance();
            user.PrivacyPolicyAcceptedAt = DateTime.UtcNow;
            user.PrivacyPolicyVersion = CurrentPrivacyPolicyVersion;

            await UserStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            var result = await UserManager.CreateAsync(user, Input.Password);

            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return Page();
            }

            _logger.LogInformation("User created a new account with password.");

            await AssignDefaultRoleAsync(user);
            await SendEmailConfirmationAsync(user, returnUrl);

            if (UserManager.Options.SignIn.RequireConfirmedAccount)
            {
                return RedirectToPage("Confirmation", new { type = "register", email = Input.Email, returnUrl });
            }

            await SignInManager.SignInAsync(user, isPersistent: false);

            // Redirect new users to the onboarding wizard
            return RedirectToAction("Step1", "Onboarding");
        }

        /// <summary>
        /// Ensures default role exists and assigns it to the user.
        /// </summary>
        private async Task AssignDefaultRoleAsync(ApplicationUser user)
        {
            if (!await _roleManager.RoleExistsAsync(DefaultUserRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(DefaultUserRole));
            }
            await UserManager.AddToRoleAsync(user, DefaultUserRole);
        }

        /// <summary>
        /// Sends email confirmation link to the new user.
        /// </summary>
        private async Task SendEmailConfirmationAsync(ApplicationUser user, string returnUrl)
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code, returnUrl },
                protocol: Request.Scheme);

            var emailBody = $"{_localizer[PleaseConfirmKey]} <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>{_localizer[ClickingHereKey]}</a>.";
            await _emailSender.SendEmailAsync(Input.Email, _localizer[ConfirmEmailKey], emailBody);
        }

        #endregion
    }
}