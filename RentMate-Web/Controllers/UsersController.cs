using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RentMate.Data;
using RentMate.Models;
using RentMate.Services;

namespace RentMate.Controllers
{
    /// <summary>
    /// Controller for user administration. Admin only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        #region Constants

        private const string AdminRole = "Admin";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly RentMateContext _context;
        private readonly IStringLocalizer<UsersController> _localizer;

        #endregion

        #region Constructor

        public UsersController(
            RentMateContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IStringLocalizer<UsersController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _localizer = localizer;
        }

        #endregion

        #region List & Details

        /// <summary>
        /// Lists all users in the system.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        /// <summary>
        /// Displays detailed information about a specific user.
        /// </summary>
        public async Task<IActionResult> Details(string? id)
        {
            var user = await FindUserByIdAsync(id);
            return user == null ? NotFound() : View(user);
        }

        #endregion

        #region Create

        /// <summary>
        /// Displays the user creation form.
        /// </summary>
        public IActionResult Create() => View();

        /// <summary>
        /// Creates a new user with the specified information.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserName,Email,FirstName,LastName,City")] ApplicationUser user)
        {
            if (!ModelState.IsValid)
            {
                return View(user);
            }

            _context.Add(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Edit

        /// <summary>
        /// Displays the edit form for a user.
        /// </summary>
        public async Task<IActionResult> Edit(string? id)
        {
            var user = await FindUserByIdAsync(id);
            return user == null ? NotFound() : View(user);
        }

        /// <summary>
        /// Updates a user's profile information.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string? id, [Bind("Id,UserName,Email,FirstName,LastName,City")] ApplicationUser updatedUser)
        {
            if (id != updatedUser.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(updatedUser);
            }

            try
            {
                var existingUser = await FindUserByIdAsync(id);
                if (existingUser == null)
                {
                    return NotFound();
                }

                UpdateUserFields(existingUser, updatedUser);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await UserExistsAsync(updatedUser.Id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        #endregion

        #region Delete

        /// <summary>
        /// Displays the delete confirmation page for a user.
        /// </summary>
        public async Task<IActionResult> Delete(string? id)
        {
            var user = await FindUserByIdAsync(id);
            return user == null ? NotFound() : View(user);
        }

        /// <summary>
        /// Deletes a user. Cannot delete admin accounts.
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string? id)
        {
            var user = await FindUserWithManagerAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (await IsAdminUserAsync(user))
            {
                return RedirectWithError(_localizer["Cannot delete an administrative account."].Value);
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Ban/Unban

        /// <summary>
        /// Bans a user by locking out their account indefinitely.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(string id)
        {
            var userToBan = await FindUserWithManagerAsync(id);
            if (userToBan == null)
            {
                return NotFound();
            }

            // Validate ban operation
            var validationError = await ValidateBanOperationAsync(userToBan);
            if (validationError != null)
            {
                return validationError;
            }

            await ApplyBanAsync(userToBan);

            TempData["SuccessMessage"] = string.Format(_localizer["User {0} has been banned."], userToBan.Email);
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Unbans a user by clearing their lockout.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbanUser(string id)
        {
            var userToUnban = await FindUserWithManagerAsync(id);
            if (userToUnban == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEndDateAsync(userToUnban, null);

            TempData["SuccessMessage"] = string.Format(_localizer["User {0} has been unbanned."], userToUnban.Email);
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Role Management

        /// <summary>
        /// Displays the role management page for a user.
        /// </summary>
        public async Task<IActionResult> ManageRoles(string id)
        {
            var user = await FindUserWithManagerAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = await BuildManageRolesViewModelAsync(user);
            return View(model);
        }

        /// <summary>
        /// Updates the roles assigned to a user.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ManageRoles(ManageRolesViewModel model)
        {
            var user = await FindUserWithManagerAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            await UpdateUserRolesAsync(user, model.Roles);

            TempData["SuccessMessage"] = string.Format(_localizer["Roles for {0} were successfully updated."], user.Email);
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Owner Info

        /// <summary>
        /// Returns a partial view with owner information for modals.
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> GetOwnerInfoPartial(string userId)
        {
            var owner = await _userManager.Users
                .Include(u => u.Items!)
                    .ThenInclude(i => i.Reviews)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (owner == null)
            {
                return NotFound();
            }

            var model = BuildOwnerModalViewModel(owner);
            return PartialView("~/Views/Shared/_OwnerInfoPartial.cshtml", model);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Finds a user by ID using the context.
        /// </summary>
        private async Task<ApplicationUser?> FindUserByIdAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return await _context.Users.FirstOrDefaultAsync(m => m.Id == id);
        }

        /// <summary>
        /// Finds a user by ID using UserManager (needed for role/lockout operations).
        /// </summary>
        private async Task<ApplicationUser?> FindUserWithManagerAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return await _userManager.FindByIdAsync(id);
        }

        /// <summary>
        /// Checks if a user exists by ID.
        /// </summary>
        private Task<bool> UserExistsAsync(string? id)
        {
            return _context.Users.AnyAsync(e => e.Id == id);
        }

        /// <summary>
        /// Checks if a user has the Admin role.
        /// </summary>
        private Task<bool> IsAdminUserAsync(ApplicationUser user)
        {
            return _userManager.IsInRoleAsync(user, AdminRole);
        }

        /// <summary>
        /// Updates basic user fields from the updated model.
        /// </summary>
        private static void UpdateUserFields(ApplicationUser existingUser, ApplicationUser updatedUser)
        {
            existingUser.Email = updatedUser.Email;
            existingUser.FirstName = updatedUser.FirstName;
            existingUser.LastName = updatedUser.LastName;
            existingUser.City = updatedUser.City;
        }

        /// <summary>
        /// Validates that a ban operation can be performed.
        /// </summary>
        private async Task<IActionResult?> ValidateBanOperationAsync(ApplicationUser userToBan)
        {
            var currentUserId = _userManager.GetUserId(User);
            
            if (userToBan.Id == currentUserId)
            {
                return RedirectWithError(_localizer["You cannot ban yourself."].Value);
            }

            if (await IsAdminUserAsync(userToBan))
            {
                return RedirectWithError(_localizer["Cannot ban an administrative account."].Value);
            }

            return null;
        }

        /// <summary>
        /// Applies an indefinite ban to a user.
        /// </summary>
        private async Task ApplyBanAsync(ApplicationUser user)
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }

        /// <summary>
        /// Builds the view model for role management.
        /// </summary>
        private async Task<ManageRolesViewModel> BuildManageRolesViewModelAsync(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = _roleManager.Roles.ToList();

            return new ManageRolesViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? string.Empty,
                Roles = allRoles.Select(role => new RoleSelection
                {
                    RoleName = role.Name,
                    IsSelected = role.Name != null && userRoles.Contains(role.Name)
                }).ToList()
            };
        }

        /// <summary>
        /// Updates user roles based on the provided selections.
        /// </summary>
        private async Task UpdateUserRolesAsync(ApplicationUser user, List<RoleSelection> roleSelections)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRoles = roleSelections
                .Where(r => r.IsSelected && r.RoleName != null)
                .Select(r => r.RoleName!)
                .ToList();

            var rolesToRemove = currentRoles.Except(selectedRoles).ToList();
            var rolesToAdd = selectedRoles.Except(currentRoles).ToList();

            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        /// <summary>
        /// Builds the view model for owner info modal.
        /// </summary>
        private static OwnerModalViewModel BuildOwnerModalViewModel(ApplicationUser owner)
        {
            var (averageRating, reviewCount) = owner.GetAllActiveReviews().CalculateRatingStats();

            return new OwnerModalViewModel
            {
                Id = owner.Id,
                FirstName = owner.FirstName,
                LastName = owner.LastName,
                City = owner.City,
                Email = owner.Email,
                ProfilePictureUrl = owner.ProfilePictureUrl,
                AverageRating = averageRating,
                ReviewCount = reviewCount,
                JoinDate = DateTime.Now.AddYears(-1) // Placeholder if JoinDate is not in the User model
            };
        }

        /// <summary>
        /// Redirects to Index with an error message.
        /// </summary>
        private IActionResult RedirectWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}