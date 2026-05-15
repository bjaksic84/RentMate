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

    /// <summary>
    /// User's primary intent: rent, list, or both. Set during onboarding Step 1.
    /// Null for legacy users who completed the old onboarding.
    /// </summary>
    public UserIntent? UserIntent { get; set; }

    /// <summary>
    /// Whether the user has completed (or dismissed) the post-onboarding spotlight tour.
    /// </summary>
    public bool SpotlightTourCompleted { get; set; }

    // ── Account deactivation ────────────────────────────────────────
    public bool IsDeactivated { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public DeactivationSource? DeactivatedBy { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? DeactivationReason { get; set; }

    // ── GDPR / Privacy ─────────────────────────────────────────────
    public string? PrivacyPolicyVersion { get; set; }
    public DateTime? PrivacyPolicyAcceptedAt { get; set; }

    // ── User preferences ────────────────────────────────────────────
    public string PreferredLanguage { get; set; } = string.Empty;
    public bool NotifyOnMessage { get; set; }
    public bool NotifyOnRentalRequest { get; set; }
    public bool NotifyOnRentalStatusChange { get; set; }
    public bool NotifyOnReview { get; set; }

    // Navigation properties
    public ICollection<Item>? Items { get; set; }
    public ICollection<Rental>? RentalsAsRenter { get; set; }
    public ICollection<Rental>? RentalsAsOwner { get; set; }
    
    /// <summary>
    /// Collection of favorited items via join entity.
    /// Use this to easily query: user.Favorites.Select(f => f.Item)
    /// </summary>
    public ICollection<AccountItemFavorite> Favorites { get; set; } = new List<AccountItemFavorite>();

    /// <summary>
    /// Loads a user by id. Maps to VOPC Uporabnik.pridobiUporabnika(int najemnikId).
    /// The id is the ASP.NET Identity string key (the design's int is a logical id).
    /// </summary>
    public static async Task<ApplicationUser?> PridobiUporabnikaAsync(
        UserManager<ApplicationUser> userManager,
        string najemnikId)
    {
        return await userManager.FindByIdAsync(najemnikId);
    }
}


