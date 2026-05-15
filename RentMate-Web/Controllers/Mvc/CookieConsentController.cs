using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentMate.Controllers.Base;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;

namespace RentMate.Controllers.Mvc;

/// <summary>
/// Records cookie consent decisions to the database.
/// Called via JavaScript fetch on all pages (authenticated and anonymous).
/// </summary>
[Route("api/cookie-consent")]
[EnableRateLimiting("ApiPolicy")]
public class CookieConsentController : BaseAppController
{
    private readonly RentMateContext _context;

    public CookieConsentController(RentMateContext context, UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _context = context;
    }

    /// <summary>
    /// Saves the user's cookie consent preferences.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] bool analytics, [FromForm] bool marketing)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var ipHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip)));

        var ua = Request.Headers.UserAgent.ToString();

        var consent = new CookieConsent
        {
            UserId = User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null,
            NecessaryCookies = true,
            AnalyticsCookies = analytics,
            MarketingCookies = marketing,
            IpAddressHash = ipHash,
            ConsentedAt = DateTime.UtcNow,
            UserAgent = string.IsNullOrEmpty(ua) ? null : ua[..Math.Min(500, ua.Length)]
        };

        _context.CookieConsents.Add(consent);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
