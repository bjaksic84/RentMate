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
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for new user registration.
    /// </summary>
    public class RegisterModel : PageModel
    {
        #region Constants

        private const int MinPasswordLength = 6;
        private const int MaxPasswordLength = 100;
        private const string DefaultUserRole = "User";
        private const string ConfirmEmailKey = "Confirm your email";
        private const string PleaseConfirmKey = "Please confirm your account by";
        private const string ClickingHereKey = "clicking here";
        private const string CreateUserErrorKey = "Can't create an instance of 'ApplicationUser'.";
        private const string EmailNotSupportedKey = "The default UI requires a user store with email support.";

        #endregion

        #region Dependencies

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserStore<ApplicationUser> _userStore;
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
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
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
        }

        #endregion

        #region Page Handlers

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

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
            var user = CreateUser();

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return Page();
            }

            _logger.LogInformation("User created a new account with password.");

            await AssignDefaultRoleAsync(user);
            await SendEmailConfirmationAsync(user, returnUrl);

            if (_userManager.Options.SignIn.RequireConfirmedAccount)
            {
                return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

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
            await _userManager.AddToRoleAsync(user, DefaultUserRole);
        }

        /// <summary>
        /// Sends email confirmation link to the new user.
        /// </summary>
        private async Task SendEmailConfirmationAsync(ApplicationUser user, string returnUrl)
        {
            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code, returnUrl },
                protocol: Request.Scheme);

            var emailBody = $"{_localizer[PleaseConfirmKey]} <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>{_localizer[ClickingHereKey]}</a>.";
            await _emailSender.SendEmailAsync(Input.Email, _localizer[ConfirmEmailKey], emailBody);
        }

        /// <summary>
        /// Adds identity errors to model state.
        /// </summary>
        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException(_localizer[CreateUserErrorKey]);
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(_localizer[EmailNotSupportedKey]);
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }

        #endregion
    }
}