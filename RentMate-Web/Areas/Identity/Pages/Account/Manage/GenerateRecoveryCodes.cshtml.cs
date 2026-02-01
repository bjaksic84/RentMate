// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for generating new 2FA recovery codes.
    /// </summary>
    public class GenerateRecoveryCodesModel : BaseIdentityPageModel
    {
        #region Constants

        private const int RecoveryCodeCount = 10;
        private const string TwoFactorNotEnabledError = "Cannot generate recovery codes for user because they do not have 2FA enabled.";
        private const string CodesGeneratedMessage = "You have generated new recovery codes.";

        #endregion

        #region Dependencies

        private readonly ILogger<GenerateRecoveryCodesModel> _logger;

        #endregion

        #region Constructor

        public GenerateRecoveryCodesModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<GenerateRecoveryCodesModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        #endregion

        #region Properties

        /// <summary>The newly generated recovery codes.</summary>
        [TempData]
        public string[] RecoveryCodes { get; set; }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await EnsureTwoFactorIsEnabledAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await EnsureTwoFactorIsEnabledAsync(user);

            var recoveryCodes = await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
            RecoveryCodes = recoveryCodes.ToArray();

            var userId = await UserManager.GetUserIdAsync(user);
            _logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", userId);
            
            SetSuccessMessage(CodesGeneratedMessage);
            return RedirectToPage("./ShowRecoveryCodes");
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Ensures 2FA is enabled before allowing recovery code generation.
        /// </summary>
        private async Task EnsureTwoFactorIsEnabledAsync(ApplicationUser user)
        {
            if (!await UserManager.GetTwoFactorEnabledAsync(user))
            {
                throw new InvalidOperationException(TwoFactorNotEnabledError);
            }
        }

        #endregion
    }
}
