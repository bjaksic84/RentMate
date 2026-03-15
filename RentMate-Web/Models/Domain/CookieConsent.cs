namespace RentMate.Models.Domain;

/// <summary>
/// Records a user's cookie consent preferences for GDPR compliance.
/// Stored for anonymous visitors (UserId = null) and authenticated users.
/// </summary>
public class CookieConsent
{
    public int Id { get; set; }

    /// <summary>Authenticated user's ID. Null for anonymous visitors.</summary>
    public string? UserId { get; set; }

    /// <summary>Necessary cookies are always accepted and cannot be declined.</summary>
    public bool NecessaryCookies { get; set; } = true;

    /// <summary>Consent for analytics cookies (usage tracking, performance).</summary>
    public bool AnalyticsCookies { get; set; }

    /// <summary>Consent for marketing cookies (personalisation, ads).</summary>
    public bool MarketingCookies { get; set; }

    /// <summary>SHA-256 hash of the visitor's IP address for audit purposes.</summary>
    public string? IpAddressHash { get; set; }

    /// <summary>When consent was recorded.</summary>
    public DateTime ConsentedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Browser user-agent string at time of consent.</summary>
    public string? UserAgent { get; set; }

    // Navigation
    public virtual ApplicationUser? User { get; set; }
}
