using Microsoft.AspNetCore.Identity;

namespace RentMate.Models.Domain;

/// <summary>
/// Extended user entity for the RentMate system.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? City { get; set; }
    public string? ProfilePictureUrl { get; set; }

    // ── Verification flags ──────────────────────────────────────────
    public bool IsPhoneVerified { get; set; }
    public bool IsGovernmentIdVerified { get; set; }
    public bool IsSocialMediaLinked { get; set; }
    public bool HasPaymentMethodAdded { get; set; }

    // ── Profile completeness signals ────────────────────────────────
    public string? Bio { get; set; }
    public bool HasReturnPolicy { get; set; }

    // ── Responsiveness tracking ─────────────────────────────────────
    /// <summary>Percentage of rental requests responded to (0–100).</summary>
    public double ResponseRate { get; set; } = 0;
    /// <summary>Average time in hours to respond to rental requests.</summary>
    public double AvgResponseTimeHours { get; set; } = 0;
    /// <summary>Total rental-request messages received (for cold-start detection).</summary>
    public int TotalMessagesReceived { get; set; } = 0;

    // ── Account maturity ────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Precomputed Profile Trust Score (0–100) ─────────────────────
    public double ProfileTrustScore { get; set; } = 0;
    public DateTime? ProfileTrustScoreUpdatedAt { get; set; }

    // ── Personalization: category affinity vector ────────────────────
    /// <summary>
    /// JSON-serialized dictionary of category → interaction count.
    /// Updated when user browses/rents items. Used for personalized ranking.
    /// </summary>
    public string? CategoryAffinityJson { get; set; }

    // ── Latitude / Longitude for geo-ranking ────────────────────────
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // ── Onboarding ──────────────────────────────────────────────────
    /// <summary>
    /// Whether the user has completed the post-registration onboarding wizard.
    /// </summary>
    public bool OnboardingCompleted { get; set; }

    // ── Preferences ───────────────────────────────────────────────────
    /// <summary>Preferred UI language ("sl" or "en"). Defaults to Slovenian.</summary>
    public string PreferredLanguage { get; set; } = "sl";

    // ── Notification Preferences ──────────────────────────────────────
    /// <summary>Receive email notifications when a new rental request is made.</summary>
    public bool NotifyOnRentalRequest { get; set; } = true;
    /// <summary>Receive email notifications for new messages.</summary>
    public bool NotifyOnMessage { get; set; } = true;
    /// <summary>Receive email notifications when a new review is posted.</summary>
    public bool NotifyOnReview { get; set; } = true;
    /// <summary>Receive email notifications for rental status changes.</summary>
    public bool NotifyOnRentalStatusChange { get; set; } = true;

    // Navigation properties
    public ICollection<Item>? Items { get; set; }
    public ICollection<Rental>? RentalsAsRenter { get; set; }
    public ICollection<Rental>? RentalsAsOwner { get; set; }
    
    /// <summary>
    /// Collection of favorited items via join entity.
    /// Use this to easily query: user.Favorites.Select(f => f.Item)
    /// </summary>
    public ICollection<AccountItemFavorite> Favorites { get; set; } = new List<AccountItemFavorite>();
}


