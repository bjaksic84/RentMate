#nullable disable

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Generic confirmation page that replaces ForgotPasswordConfirmation,
    /// ResetPasswordConfirmation, and RegisterConfirmation.
    /// </summary>
    [AllowAnonymous]
    public class ConfirmationModel : PageModel
    {
        #region Constants

        private const string UserNotFoundKey = "Unable to load user with email '{0}'.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _sender;
        private readonly IStringLocalizer<ConfirmationModel> _localizer;

        #endregion

        #region Constructor

        public ConfirmationModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender sender,
            IStringLocalizer<ConfirmationModel> localizer)
        {
            _userManager = userManager;
            _sender = sender;
            _localizer = localizer;
        }

        #endregion

        #region Properties

        /// <summary>Confirmation type: password-reset-sent, password-reset, or register.</summary>
        [BindProperty(SupportsGet = true)]
        public string Type { get; set; }

        /// <summary>Email address (used by register type).</summary>
        public string Email { get; set; }

        /// <summary>Whether to show the direct confirmation link (development/testing).</summary>
        public bool DisplayConfirmAccountLink { get; set; }

        /// <summary>Direct email confirmation URL for dev mode.</summary>
        public string EmailConfirmationUrl { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync(string email = null, string returnUrl = null)
        {
            if (Type != "password-reset-sent" && Type != "password-reset" && Type != "register")
                return RedirectToPage("/Index");

            if (Type == "register")
                return await HandleRegisterConfirmationAsync(email, returnUrl);

            return Page();
        }

        #endregion

        #region Private Helpers

        private async Task<IActionResult> HandleRegisterConfirmationAsync(string email, string returnUrl)
        {
            if (email == null)
                return RedirectToPage("/Index");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return NotFound(string.Format(_localizer[UserNotFoundKey], email));

            Email = email;

            // TODO: Remove this code when using a real email sender
            DisplayConfirmAccountLink = true;

            if (DisplayConfirmAccountLink)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                EmailConfirmationUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId, code, returnUrl = returnUrl ?? Url.Content("~/") },
                    protocol: Request.Scheme);
            }

            return Page();
        }

        #endregion
    }
}
