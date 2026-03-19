using Microsoft.AspNetCore.Identity;
using RentMate.Models.Domain;

namespace RentMate.Infrastructure.Middleware;

/// <summary>
/// Middleware that blocks deactivated users from accessing the application.
/// Runs for ALL request types (MVC controllers, Razor Pages, API endpoints).
/// Replaces the old <c>DeactivatedAccountFilter</c> which only covered MVC controllers.
///
/// For browser requests: redirects to /Account/Deactivated.
/// For API requests: returns 403 Forbidden with a JSON body.
/// </summary>
public class DeactivatedAccountMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Exact paths that deactivated users are always allowed to visit.
    /// </summary>
    private static readonly HashSet<string> AllowedExactPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Deactivated",
        "/Account/Reactivate",
        "/Identity/Account/Logout",
        "/Identity/Account/Login",
        "/api/cookie-consent"
    };

    /// <summary>
    /// Path prefixes that deactivated users are allowed to visit (for multi-route areas).
    /// </summary>
    private static readonly string[] AllowedPathPrefixes =
    [
        "/api/Auth/"
    ];

    public DeactivatedAccountMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip anonymous requests
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Skip allowed paths (exact match first, then prefix match)
        var path = context.Request.Path.Value ?? string.Empty;
        if (AllowedExactPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        foreach (var allowed in AllowedPathPrefixes)
        {
            if (path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        // Skip static files
        if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);

        if (user?.IsDeactivated == true)
        {
            // API requests get a 403 JSON response
            if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Account is deactivated.\"}");
                return;
            }

            // Browser requests get redirected
            context.Response.Redirect("/Account/Deactivated");
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the <see cref="DeactivatedAccountMiddleware"/>.
/// </summary>
public static class DeactivatedAccountMiddlewareExtensions
{
    public static IApplicationBuilder UseDeactivatedAccountCheck(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DeactivatedAccountMiddleware>();
    }
}
