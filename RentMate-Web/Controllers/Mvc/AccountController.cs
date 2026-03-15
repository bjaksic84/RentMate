using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Controllers.Mvc
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAccountLifecycleService _accountLifecycle;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAccountLifecycleService accountLifecycle)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _accountLifecycle = accountLifecycle;
        }

        [HttpGet("/AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View();
        }

        /// <summary>
        /// Shown to authenticated users whose account is deactivated.
        /// The <see cref="DeactivatedAccountFilter"/> redirects here automatically.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Deactivated()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Not actually deactivated — send home
            if (!user.IsDeactivated)
                return RedirectToAction("Index", "Home");

            return View(user);
        }

        /// <summary>
        /// Reactivates a user-deactivated account. Admin-deactivated accounts are rejected.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!user.IsDeactivated)
                return RedirectToAction("Index", "Home");

            if (user.DeactivatedBy == DeactivationSource.Admin)
            {
                TempData["ErrorMessage"] = "Your account was deactivated by an administrator and cannot be self-reactivated. Please submit a reactivation request.";
                return RedirectToAction(nameof(Deactivated));
            }

            await _accountLifecycle.ReactivateAccountAsync(user.Id);

            // Refresh the authentication cookie so the filter no longer intercepts
            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "Your account has been reactivated. Welcome back!";
            return RedirectToAction("Index", "Home");
        }
    }
}

