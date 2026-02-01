// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Page model for user logout.
    /// </summary>
    public class LogoutModel : PageModel
    {
        #region Dependencies

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        #endregion

        #region Constructor

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager, 
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");

            // Redirect to force a new request and update the user identity
            return returnUrl != null 
                ? LocalRedirect(returnUrl) 
                : RedirectToPage();
        }

        #endregion
    }
}