// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for displaying newly generated recovery codes.
    /// </summary>
    public class ShowRecoveryCodesModel : PageModel
    {
        /// <summary>The recovery codes to display to the user.</summary>
        [TempData]
        public string[] RecoveryCodes { get; set; }

        /// <summary>Status message for display.</summary>
        [TempData]
        public string StatusMessage { get; set; }

        public IActionResult OnGet()
        {
            if (RecoveryCodes == null || RecoveryCodes.Length == 0)
            {
                return RedirectToPage("./TwoFactorAuthentication");
            }

            return Page();
        }
    }
}
