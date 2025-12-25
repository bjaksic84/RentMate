using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<ConfirmEmailModel> _localizer;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager, IStringLocalizer<ConfirmEmailModel> localizer)
        {
            _userManager = userManager;
            _localizer = localizer;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(string.Format(_localizer["Unable to load user with ID '{0}'."], userId));
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);
            
            // These strings are keys that must exist in the .resx
            StatusMessage = result.Succeeded 
                ? _localizer["Thank you for confirming your email."] 
                : _localizer["Error confirming your email."];
                
            return Page();
        }
    }
}