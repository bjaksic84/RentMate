namespace RentMate.Services.Interfaces;

/// <summary>
/// Service for computing Profile Trust Scores and Item Scores
/// as defined by the Marketplace Ranking System v2.
/// </summary>
public interface IScoringService
{
    // ── Profile Trust Score ─────────────────────────────────────────

    /// <summary>Recompute and persist the Profile Trust Score for a single user.</summary>
    Task<double> ComputeAndSaveProfileTrustScoreAsync(string userId);

    /// <summary>Recompute Profile Trust Scores for all users (batch job).</summary>
    Task<int> RecomputeAllProfileTrustScoresAsync(CancellationToken ct = default);

    // ── Item Score ──────────────────────────────────────────────────

    /// <summary>Recompute and persist the Item Score for a single item.</summary>
    Task<double> ComputeAndSaveItemScoreAsync(int itemId);

    /// <summary>Recompute Item Scores for all listed items (batch job).</summary>
    Task<int> RecomputeAllItemScoresAsync(CancellationToken ct = default);

    // ── Convenience: recompute everything ───────────────────────────

    /// <summary>Full batch recalculation of all profile and item scores.</summary>
    Task RunFullRecalculationAsync(CancellationToken ct = default);

    // ── Global mean maintenance ─────────────────────────────────────

    /// <summary>Recalculate the platform-wide global mean rating (C) from all reviews.</summary>
    Task RecalculateGlobalMeanAsync();

    // ── View tracking ───────────────────────────────────────────────

    /// <summary>Record a page-view for an item (increments TotalViews).</summary>
    Task RecordItemViewAsync(int itemId);

    /// <summary>Batch-refresh ViewsLast30Days for all items.</summary>
    Task RefreshViewCountsAsync(CancellationToken ct = default);

    // ── Seller dashboard ────────────────────────────────────────────

    /// <summary>Get a breakdown of Profile Trust Score components for dashboard display.</summary>
    Task<ProfileTrustBreakdown> GetProfileTrustBreakdownAsync(string userId);

    /// <summary>Get a breakdown of Item Score components for dashboard display.</summary>
    Task<ItemScoreBreakdown> GetItemScoreBreakdownAsync(int itemId);

    // ── Anti-gaming ─────────────────────────────────────────────────

    /// <summary>
    /// Detect review velocity anomalies for a user.
    /// Returns true if the user's recent review intake exceeds 3× their historical rate.
    /// </summary>
    Task<bool> DetectReviewVelocityAnomalyAsync(string userId);

    // ── Personalization ─────────────────────────────────────────────

    /// <summary>Record a category interaction for personalization (browse, rent).</summary>
    Task RecordCategoryInteractionAsync(string userId, string category);

    /// <summary>Get a personalized boost multiplier for an item given the current user.</summary>
    Task<double> GetPersonalizedBoostAsync(string? userId, string? itemCategory);
}

// ── Breakdown DTOs for seller dashboard ──────────────────────────────

/// <summary>Component-level breakdown of a Profile Trust Score.</summary>
public record ProfileTrustBreakdown
{
    public double TotalScore { get; init; }
    public double VerificationScore { get; init; }
    public double ReputationScore { get; init; }
    public double ActivityScore { get; init; }
    public double ResponsivenessScore { get; init; }
    public double ProfileCompletenessScore { get; init; }
    public double AccountMaturityScore { get; init; }
    public double DisputePenalty { get; init; }
    public List<string> ImprovementTips { get; init; } = new();
}

/// <summary>Component-level breakdown of an Item Score.</summary>
public record ItemScoreBreakdown
{
    public double TotalScore { get; init; }
    public double ItemRatingScore { get; init; }
    public double OwnerTrustComponent { get; init; }
    public double ContentQualityScore { get; init; }
    public double PhotoScore { get; init; }
    public double PricingScore { get; init; }
    public double DemandScore { get; init; }
    public double FreshnessScore { get; init; }
    public double NewListingBoostMultiplier { get; init; }
    public double SeasonalBoost { get; init; }
    public List<string> ImprovementTips { get; init; } = new();
}
