using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentMate.Models;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RentMate.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("ApiPolicy")]
    public class UsersApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IStringLocalizer<UsersApiController> _localizer;

        public UsersApiController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IStringLocalizer<UsersApiController> localizer)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _localizer = localizer;
        }

        // Assign a role to a user
        [HttpPost("{userId}/roles/{roleName}")]
        public async Task<IActionResult> AddRoleToUser(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(_localizer["User not found."].Value);

            if (!await _roleManager.RoleExistsAsync(roleName))
                return BadRequest(_localizer["Role does not exist."].Value);

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = string.Format(_localizer["Role '{0}' assigned to user '{1}'."], roleName, user.UserName) });
        }

        // Remove a role
        [HttpDelete("{userId}/roles/{roleName}")]
        public async Task<IActionResult> RemoveRoleFromUser(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(_localizer["User not found."].Value);

            if (!await _roleManager.RoleExistsAsync(roleName))
                return BadRequest(_localizer["Role does not exist."].Value);

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = string.Format(_localizer["Role '{0}' removed from user '{1}'."], roleName, user.UserName) });
        }

        // List roles of a user
        [HttpGet("{userId}/roles")]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(_localizer["User not found."].Value);

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(roles);
        }

        // List all users with their roles (handy for admin UI / Swagger)
        [HttpGet]
        public async Task<IActionResult> GetAllUsersWithRoles()
        {
            var users = _userManager.Users.ToList();
            var result = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Roles = roles
                });
            }

            return Ok(result);
        }

        [HttpPost("{userId}/roles/manage")]
        public async Task<IActionResult> ManageUserRoles(string userId, [FromBody] List<string> roles)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(_localizer["User not found."].Value);

            var currentRoles = await _userManager.GetRolesAsync(user);

            var rolesToAdd = roles.Except(currentRoles);
            var rolesToRemove = currentRoles.Except(roles);

            await _userManager.AddToRolesAsync(user, rolesToAdd);
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

            return Ok();
        }

        [HttpGet("roles")]
        public IActionResult GetAllRoles()
        {
            var roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return Ok(roles);
        }

        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(_localizer["User not found."].Value);

            // Prevent admin from deleting themselves
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == userId) 
                return BadRequest(_localizer["You cannot delete yourself."].Value);

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded) return Ok();
            
            return BadRequest(result.Errors);
        }
    }
}