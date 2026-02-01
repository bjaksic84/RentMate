namespace RentMate.Infrastructure.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses.
/// Implements OWASP security best practices.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Remove server identification headers
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");
        
        // Prevent clickjacking
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        // Enable XSS filtering
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        
        // Referrer Policy
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Content Security Policy
        context.Response.Headers.Append("Content-Security-Policy", BuildContentSecurityPolicy());
        
        // Permissions Policy (GDPR compliant)
        context.Response.Headers.Append("Permissions-Policy", BuildPermissionsPolicy());

        await _next(context);
    }

    private static string BuildContentSecurityPolicy() =>
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
        "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
        "img-src 'self' data: https: blob:; " +
        "connect-src 'self' https: wss:; " +
        "frame-ancestors 'none';";

    private static string BuildPermissionsPolicy() =>
        "accelerometer=(), camera=(self), geolocation=(self), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
}

/// <summary>
/// Extension methods for registering security headers middleware.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds security headers middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
