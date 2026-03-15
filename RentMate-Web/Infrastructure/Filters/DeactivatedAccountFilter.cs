using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RentMate.Models.Domain;

namespace RentMate.Infrastructure.Filters;

/// <summary>
/// Global action filter that intercepts every authenticated request and redirects
/// deactivated users to the <c>/Account/Deactivated</c> page.
///
/// Allowed paths (deactivated users may always access these):
/// <list type="bullet">
///   <item>/Account/Deactivated</item>
///   <item>/Account/Reactivate</item>
///   <item>/Identity/Account/Logout</item>
///   <item>/api/cookie-consent</item>
/// </list>
/// </summary>
public class DeactivatedAccountFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _userManager;

    // Routes that deactivated users are always allowed to visit
    private static readonly HashSet<string> _allowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Deactivated",
        "/Account/Reactivate",
        "/Identity/Account/Logout",
        "/api/cookie-consent"
    };

    public DeactivatedAccountFilter(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        // Skip anonymous requests
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            await next();
            return;
        }

        // Skip allowed paths
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (_allowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        var user = await _userManager.GetUserAsync(httpContext.User);
        if (user?.IsDeactivated == true)
        {
            context.Result = new RedirectToActionResult("Deactivated", "Account", null);
            return;
        }

        await next();
    }
}
