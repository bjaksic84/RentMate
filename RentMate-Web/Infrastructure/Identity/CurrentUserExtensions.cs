using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RentMate.Models.Domain;

namespace RentMate.Infrastructure.Identity
{
    /// <summary>
    /// Single source of truth for resolving the current authenticated <see cref="ApplicationUser"/>.
    /// Both <c>BaseAppController</c> and <c>BaseIdentityPageModel</c> forward to these helpers
    /// so the lookup logic lives in exactly one place.
    /// </summary>
    public static class CurrentUserExtensions
    {
        public static Task<ApplicationUser?> GetCurrentUserAsync(
            this UserManager<ApplicationUser> userManager, ClaimsPrincipal user)
        {
            return userManager.GetUserAsync(user);
        }

        public static string? GetCurrentUserId(
            this UserManager<ApplicationUser> userManager, ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }
    }
}
