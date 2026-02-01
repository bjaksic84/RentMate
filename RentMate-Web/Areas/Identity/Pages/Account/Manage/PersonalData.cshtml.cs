// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for the personal data overview page.
    /// </summary>
    public class PersonalDataModel : BaseIdentityPageModel
    {
        private readonly ILogger<PersonalDataModel> _logger;

        public PersonalDataModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<PersonalDataModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            return errorResult ?? Page();
        }
    }
}
