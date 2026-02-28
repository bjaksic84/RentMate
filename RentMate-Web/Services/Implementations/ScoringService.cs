using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Services.Implementations;

/// <summary>
/// Full implementation of the Marketplace Ranking System v2.
/// Computes Profile Trust Scores (0–100) and Item Scores (0.0–1.0)
/// using Bayesian-adjusted ratings, weighted composite signals,
/// anti-gaming checks, dispute penalties, seasonal adjustments,
/// geo-ranking, and personalization.
/// </summary>
public class ScoringService : IScoringService
{
    #region Constants

    // ── Bayesian-average parameters ─────────────────────────────────
    private const double M_ITEMS = 5.0;   // phantom votes for item reviews
    private const double M_USERS = 10.0;  // phantom votes for user reviews
    private const double DEFAULT_GLOBAL_MEAN = 3.5;

    // ── Review time-decay half-life (days) ──────────────────────────
    private const double REVIEW_HALF_LIFE_DAYS = 90.0;

    // ── Freshness half-life (days) ──────────────────────────────────
    private const double FRESHNESS_HALF_LIFE = 21.0;

    // ── New listing boost ───────────────────────────────────────────
    private const double BOOST_WINDOW_DAYS = 7.0;
    private const double BOOST_HALF_LIFE = 3.0;
    private const double BOOST_MAX = 0.30;

    // ── Activity reference value ────────────────────────────────────
    private const double ACTIVITY_REF = 50.0;

    // ── Demand reference value ──────────────────────────────────────
    private const double DEMAND_REF = 30.0;

    // ── Account maturity sigmoid centre (days) ──────────────────────
    private const double MATURITY_CENTER_DAYS = 90.0;
    private const double MATURITY_SCALE = 60.0;

    // ── Responsiveness decay ────────────────────────────────────────
    private const double RESPONSE_DECAY = 0.05;

    // ── Pricing sigmoid steepness ───────────────────────────────────
    private const double PRICING_STEEPNESS = 3.0;

    // ── Anti-gaming: review velocity multiplier threshold ────────────
    private const double VELOCITY_MULTIPLIER_THRESHOLD = 3.0;

    // ── Diversity: max items per owner in top 20 ────────────────────
    public const int MAX_OWNER_ITEMS_TOP20 = 3;

    // ── Personalization cap ─────────────────────────────────────────
    private const double PERSONALIZATION_BOOST_MAX = 0.15;

    // ── Seasonal relevance: category → months with boosted relevance
    private static readonly Dictionary<string, HashSet<int>> SeasonalCategories = new()
    {
        ["Sports"]  = new() { 5, 6, 7, 8 },          // summer
        ["Garden"]  = new() { 4, 5, 6, 7, 8, 9 },    // spring–autumn
        ["Events"]  = new() { 6, 7, 8, 12 },          // summer + holiday season
        ["Tools"]   = new() { 3, 4, 5, 9, 10 },       // spring + autumn DIY
    };
    private const double SEASONAL_BOOST_MAX = 0.10;

    // ── Minimum ItemScore for sponsored eligibility ─────────────────
    public const double MIN_ITEM_SCORE_FOR_SPONSORED = 0.30;

    #endregion

    #region Dependencies

    private readonly RentMateContext _context;
    private readonly ILogger<ScoringService> _logger;
    private double _globalMeanRating = DEFAULT_GLOBAL_MEAN;

    #endregion

    #region Constructor

    public ScoringService(RentMateContext context, ILogger<ScoringService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #endregion

    // ================================================================
    //  PUBLIC API — Profile Trust Score
    // ================================================================

    #region Profile Trust Score

    /// <inheritdoc/>
    public async Task<double> ComputeAndSaveProfileTrustScoreAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return 0;

        var score = await ComputeProfileTrustScoreAsync(user);

        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.ProfileTrustScore, Math.Round(score, 2))
                .SetProperty(u => u.ProfileTrustScoreUpdatedAt, DateTime.UtcNow));

        return score;
    }

    /// <inheritdoc/>
    public async Task<int> RecomputeAllProfileTrustScoresAsync(CancellationToken ct = default)
    {
        var userIds = await _context.Users.Select(u => u.Id).ToListAsync(ct);
        int count = 0;
        foreach (var userId in userIds)
        {
            ct.ThrowIfCancellationRequested();
            await ComputeAndSaveProfileTrustScoreAsync(userId);
            count++;
        }
        _logger.LogInformation("Recomputed Profile Trust Scores for {Count} users", count);
        return count;
    }

    /// <summary>Core computation of Profile Trust Score (0–100).</summary>
    private async Task<double> ComputeProfileTrustScoreAsync(ApplicationUser user)
    {
        // 3.1 VerificationScore
        double verificationScore =
              (user.EmailConfirmed ? 1 : 0) * 0.15
            + (user.IsPhoneVerified ? 1 : 0) * 0.20
            + (user.IsGovernmentIdVerified ? 1 : 0) * 0.35
            + (!string.IsNullOrEmpty(user.ProfilePictureUrl) ? 1 : 0) * 0.15
            + (user.IsSocialMediaLinked ? 1 : 0) * 0.10
            + (user.HasPaymentMethodAdded ? 1 : 0) * 0.05;

        // 3.2 ReputationScore (Bayesian-adjusted, time-weighted, with dispute penalty)
        var (reputationScore, disputePenalty) = await ComputeUserReputationAsync(user.Id);

        // 3.3 ActivityScore
        int completedRentals = await _context.Rentals
            .CountAsync(r => (r.OwnerId == user.Id || r.RenterId == user.Id)
                          && r.Status == RentalStatus.Completed);
        double activityScore = completedRentals > 0
            ? Math.Log(1 + completedRentals) / Math.Log(1 + ACTIVITY_REF)
            : 0;
        activityScore = Math.Min(activityScore, 1.0);

        // 3.4 ResponsivenessScore
        double responsivenessScore;
        if (user.TotalMessagesReceived == 0)
        {
            responsivenessScore = 0.5; // neutral default
        }
        else
        {
            double responseRate01 = user.ResponseRate / 100.0;
            double responseTimeScore = Math.Exp(-RESPONSE_DECAY * user.AvgResponseTimeHours);
            responsivenessScore = 0.6 * responseRate01 + 0.4 * responseTimeScore;
        }

        // 3.5 ProfileCompletenessScore
        bool hasListedItems = await _context.Items.AnyAsync(i => i.UserId == user.Id && i.IsListed);
        double profileCompletenessScore =
              (!string.IsNullOrEmpty(user.ProfilePictureUrl) ? 1 : 0) * 0.30
            + (!string.IsNullOrEmpty(user.Bio) ? 1 : 0) * 0.20
            + (!string.IsNullOrEmpty(user.City) ? 1 : 0) * 0.15
            + (hasListedItems ? 1 : 0) * 0.20
            + (user.HasReturnPolicy ? 1 : 0) * 0.15;

        // 3.6 AccountMaturityScore (sigmoid)
        double accountAgeDays = (DateTime.UtcNow - user.CreatedAt).TotalDays;
        double accountMaturityScore = 1.0 / (1.0 + Math.Exp(-(accountAgeDays - MATURITY_CENTER_DAYS) / MATURITY_SCALE));

        // Weighted composite
        double rawScore = 100.0 * (
            0.15 * verificationScore +
            0.30 * reputationScore +
            0.20 * activityScore +
            0.15 * responsivenessScore +
            0.10 * profileCompletenessScore +
            0.10 * accountMaturityScore
        );

        return Math.Clamp(rawScore, 0, 100);
    }

    /// <summary>
    /// Compute time-weighted Bayesian user reputation with dispute penalty.
    /// Returns (reputationScore 0–1, disputePenalty 0–0.30).
    /// </summary>
    private async Task<(double ReputationScore, double DisputePenalty)> ComputeUserReputationAsync(string userId)
    {
        // All reviews for items owned by this user
        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Item != null && r.Item.UserId == userId)
            .Select(r => new { r.Rating, r.CreatedAt, ReviewerTrustScore = r.Reviewer != null ? r.Reviewer.ProfileTrustScore : 50 })
            .ToListAsync();

        double weightedSum = 0;
        double weightedCount = 0;
        foreach (var rev in reviews)
        {
            double ageDays = (DateTime.UtcNow - rev.CreatedAt).TotalDays;
            double timeDecay = Math.Exp(-Math.Log(2) * ageDays / REVIEW_HALF_LIFE_DAYS);
            // Weight by reviewer trust (§11.1)
            double reviewerTrustWeight = rev.ReviewerTrustScore / 100.0;
            double weight = timeDecay * reviewerTrustWeight;
            weightedSum += rev.Rating * weight;
            weightedCount += weight;
        }

        double bayesianRating;
        if (weightedCount > 0)
        {
            double wAvg = weightedSum / weightedCount;
            bayesianRating = (weightedCount * wAvg + M_USERS * _globalMeanRating)
                           / (weightedCount + M_USERS);
        }
        else
        {
            bayesianRating = _globalMeanRating;
        }

        double reputationScore = (bayesianRating - 1.0) / 4.0;

        // §11.5 Dispute penalty
        int completedRentals = await _context.Rentals
            .CountAsync(r => r.OwnerId == userId && r.Status == RentalStatus.Completed);

        // Negotiated disputes (Disputed status resolved)
        int negotiatedDisputes = await _context.RentalDeposits
            .CountAsync(d => d.Rental != null && d.Rental.OwnerId == userId
                          && (d.Status == DepositStatus.Released
                              || d.Status == DepositStatus.PartiallyCharged)
                          && d.DisputedAt != null);

        // Admin ruled against this owner
        int adminRuledAgainst = await _context.RentalDeposits
            .CountAsync(d => d.Rental != null && d.Rental.OwnerId == userId
                          && d.Status == DepositStatus.Released
                          && d.AdminResolvedAt != null);

        double weightedDisputeCount = negotiatedDisputes * 0.3 + adminRuledAgainst * 1.0;
        double disputePenalty = Math.Min(weightedDisputeCount / (completedRentals + 1), 0.3);

        reputationScore *= (1.0 - disputePenalty);

        return (Math.Clamp(reputationScore, 0, 1), disputePenalty);
    }

    #endregion

    // ================================================================
    //  PUBLIC API — Item Score
    // ================================================================

    #region Item Score

    /// <inheritdoc/>
    public async Task<double> ComputeAndSaveItemScoreAsync(int itemId)
    {
        var item = await _context.Items
            .AsNoTracking()
            .Include(i => i.Images)
            .Include(i => i.Reviews.Where(r => !r.IsDeleted))
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null) return 0;

        var score = await ComputeItemScoreAsync(item);

        await _context.Items
            .Where(i => i.Id == itemId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.ItemScore, Math.Round(score, 6))
                .SetProperty(i => i.ItemScoreUpdatedAt, DateTime.UtcNow));

        return score;
    }

    /// <inheritdoc/>
    public async Task<int> RecomputeAllItemScoresAsync(CancellationToken ct = default)
    {
        // Preload category median prices for PricingScore
        await RefreshCategoryMediansAsync();

        var itemIds = await _context.Items
            .Where(i => i.IsListed && !i.IsAdminHidden)
            .Select(i => i.Id)
            .ToListAsync(ct);

        int count = 0;
        foreach (var id in itemIds)
        {
            ct.ThrowIfCancellationRequested();
            await ComputeAndSaveItemScoreAsync(id);
            count++;
        }
        _logger.LogInformation("Recomputed Item Scores for {Count} items", count);
        return count;
    }

    /// <summary>Core computation of Item Score (0.0–1.0).</summary>
    private async Task<double> ComputeItemScoreAsync(Item item)
    {
        // 4.1 ItemRatingScore (time-weighted Bayesian)
        double ratingScore = ComputeTimeWeightedBayesianItemRating(item);

        // 4.2 OwnerTrustComponent
        double ownerTrust = (item.User?.ProfileTrustScore ?? 0) / 100.0;

        // 4.3 ContentQualityScore
        double contentScore = ComputeContentQualityScore(item);

        // 4.4 PhotoScore
        int photoCount = item.Images?.Count ?? 0;
        if (photoCount == 0 && !string.IsNullOrEmpty(item.ImageUrl))
            photoCount = 1; // legacy single image
        double photoScore = 1.0 - Math.Exp(-0.5 * photoCount);

        // 4.5 PricingScore (sigmoid vs category median)
        double pricingScore = await ComputePricingScoreAsync(item);

        // 4.6 DemandScore
        int completedRentals = await _context.Rentals
            .CountAsync(r => r.ItemId == item.Id && r.Status == RentalStatus.Completed);
        double demandScore = Math.Log(1 + completedRentals + 0.5 * item.ViewsLast30Days / 100.0)
                           / Math.Log(1 + DEMAND_REF);
        demandScore = Math.Min(demandScore, 1.0);

        // 4.7 FreshnessScore
        double daysSinceActivity = (DateTime.UtcNow - item.LastActivityDate).TotalDays;
        double freshnessScore = Math.Exp(-Math.Log(2) * daysSinceActivity / FRESHNESS_HALF_LIFE);

        // Weighted composite
        double baseScore = 0.25 * ratingScore
                         + 0.15 * ownerTrust
                         + 0.15 * contentScore
                         + 0.10 * photoScore
                         + 0.15 * pricingScore
                         + 0.10 * demandScore
                         + 0.10 * freshnessScore;

        // §5 New listing boost
        double daysSinceListed = (DateTime.UtcNow - item.CreatedAt).TotalDays;
        double boostMultiplier = 1.0;
        if (daysSinceListed <= BOOST_WINDOW_DAYS)
        {
            double decay = Math.Exp(-Math.Log(2) * daysSinceListed / BOOST_HALF_LIFE);
            boostMultiplier = 1.0 + BOOST_MAX * decay;
        }

        // §11.6 Seasonal boost
        double seasonalBoost = ComputeSeasonalBoost(item.Category);

        double finalScore = baseScore * boostMultiplier * (1.0 + seasonalBoost);

        return Math.Clamp(finalScore, 0.0, 1.0);
    }

    /// <summary>Time-weighted Bayesian average for item reviews (§6.1).</summary>
    private double ComputeTimeWeightedBayesianItemRating(Item item)
    {
        var reviews = item.Reviews?.Where(r => !r.IsDeleted).ToList();
        if (reviews == null || reviews.Count == 0)
        {
            // No reviews → global mean
            return (_globalMeanRating - 1.0) / 4.0;
        }

        double weightedSum = 0;
        double weightedCount = 0;
        foreach (var rev in reviews)
        {
            double ageDays = (DateTime.UtcNow - rev.CreatedAt).TotalDays;
            double timeDecay = Math.Exp(-Math.Log(2) * ageDays / REVIEW_HALF_LIFE_DAYS);
            weightedSum += rev.Rating * timeDecay;
            weightedCount += timeDecay;
        }

        double wAvg = weightedSum / weightedCount;
        double bayesianRating = (weightedCount * wAvg + M_ITEMS * _globalMeanRating)
                              / (weightedCount + M_ITEMS);

        return (bayesianRating - 1.0) / 4.0;
    }

    /// <summary>Content quality composite (§4.3 + §11.7 deposit signal).</summary>
    private static double ComputeContentQualityScore(Item item)
    {
        double titleLen = item.Title?.Length ?? 0;
        double descLen = item.Description?.Length ?? 0;
        double titleScore = Math.Clamp(titleLen / 60.0, 0, 1);
        double descScore = Math.Clamp(descLen / 300.0, 0, 1);

        return 0.25 * titleScore
             + 0.30 * descScore          // reduced from 0.35 to accommodate deposit signal
             + 0.10 * (item.Category != null && item.Category != "Uncategorized" ? 1 : 0)
             + 0.10 * (!string.IsNullOrEmpty(item.Condition) ? 1 : 0)
             + 0.15 * (item.HasAvailability ? 1 : 0)
             + 0.10 * (item.DepositAmount.HasValue && item.DepositAmount > 0 ? 1 : 0);
    }

    /// <summary>Pricing score: sigmoid vs category median (§4.5).</summary>
    private async Task<double> ComputePricingScoreAsync(Item item)
    {
        if (item.Price == null || item.Price <= 0) return 0.5;

        decimal? median = await GetCategoryMedianPriceAsync(item.Category);
        if (median == null || median <= 0) return 0.5; // not enough data

        double pctDiff = (double)((item.Price.Value - median.Value) / median.Value);
        return 1.0 / (1.0 + Math.Exp(PRICING_STEEPNESS * pctDiff));
    }

    /// <summary>Seasonal boost (§11.6).</summary>
    private static double ComputeSeasonalBoost(string? category)
    {
        if (string.IsNullOrEmpty(category)) return 0;
        if (!SeasonalCategories.TryGetValue(category, out var months)) return 0;
        int currentMonth = DateTime.UtcNow.Month;
        return months.Contains(currentMonth) ? SEASONAL_BOOST_MAX : 0;
    }

    #endregion

    // ================================================================
    //  PUBLIC API — Global Mean
    // ================================================================

    #region Global Mean

    /// <inheritdoc/>
    public async Task RecalculateGlobalMeanAsync()
    {
        var mean = await _context.Reviews
            .Where(r => !r.IsDeleted)
            .AverageAsync(r => (double?)r.Rating);

        _globalMeanRating = mean ?? DEFAULT_GLOBAL_MEAN;
        _logger.LogInformation("Global mean rating recalculated: {Mean:F2}", _globalMeanRating);
    }

    #endregion

    // ================================================================
    //  PUBLIC API — Full Recalculation
    // ================================================================

    #region Full Recalculation

    /// <inheritdoc/>
    public async Task RunFullRecalculationAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting full scoring recalculation");

        await RecalculateGlobalMeanAsync();
        await RefreshViewCountsAsync(ct);
        int users = await RecomputeAllProfileTrustScoresAsync(ct);
        int items = await RecomputeAllItemScoresAsync(ct);

        _logger.LogInformation("Full recalculation complete: {Users} users, {Items} items", users, items);
    }

    #endregion

    // ================================================================
    //  PUBLIC API — View Tracking
    // ================================================================

    #region View Tracking

    /// <inheritdoc/>
    public async Task RecordItemViewAsync(int itemId)
    {
        await _context.Items
            .Where(i => i.Id == itemId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.TotalViews, i => i.TotalViews + 1));
    }

    /// <inheritdoc/>
    public async Task RefreshViewCountsAsync(CancellationToken ct = default)
    {
        // ViewsLast30Days = count of views in last 30 days.
        // Since we only track TotalViews (lifetime), we can't do a perfect 30-day window
        // without a separate view-log table. For now, use a heuristic:
        // keep ViewsLast30Days as-is (it gets incremented by RecordItemViewAsync
        // and gradually decayed by the batch job).
        // A proper implementation would use a time-series views table.
        // For now, apply a decay factor: multiply by 0.9 each batch run (≈4-6h).
        await _context.Items
            .Where(i => i.IsListed && !i.IsAdminHidden)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.ViewsLast30Days, i => (int)(i.ViewsLast30Days * 0.97)),
            ct);

        _logger.LogDebug("Refreshed ViewsLast30Days with decay factor");
    }

    #endregion

    // ================================================================
    //  PUBLIC API — Seller Dashboard Breakdowns
    // ================================================================

    #region Seller Dashboard

    /// <inheritdoc/>
    public async Task<ProfileTrustBreakdown> GetProfileTrustBreakdownAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return new ProfileTrustBreakdown();

        // Recompute components individually for display
        double verificationScore =
              (user.EmailConfirmed ? 1 : 0) * 0.15
            + (user.IsPhoneVerified ? 1 : 0) * 0.20
            + (user.IsGovernmentIdVerified ? 1 : 0) * 0.35
            + (!string.IsNullOrEmpty(user.ProfilePictureUrl) ? 1 : 0) * 0.15
            + (user.IsSocialMediaLinked ? 1 : 0) * 0.10
            + (user.HasPaymentMethodAdded ? 1 : 0) * 0.05;

        var (reputationScore, disputePenalty) = await ComputeUserReputationAsync(user.Id);

        int completedRentals = await _context.Rentals
            .CountAsync(r => (r.OwnerId == user.Id || r.RenterId == user.Id)
                          && r.Status == RentalStatus.Completed);
        double activityScore = completedRentals > 0
            ? Math.Min(Math.Log(1 + completedRentals) / Math.Log(1 + ACTIVITY_REF), 1.0)
            : 0;

        double responsivenessScore;
        if (user.TotalMessagesReceived == 0)
            responsivenessScore = 0.5;
        else
        {
            double responseRate01 = user.ResponseRate / 100.0;
            double responseTimeScore = Math.Exp(-RESPONSE_DECAY * user.AvgResponseTimeHours);
            responsivenessScore = 0.6 * responseRate01 + 0.4 * responseTimeScore;
        }

        bool hasListedItems = await _context.Items.AnyAsync(i => i.UserId == user.Id && i.IsListed);
        double profileCompletenessScore =
              (!string.IsNullOrEmpty(user.ProfilePictureUrl) ? 1 : 0) * 0.30
            + (!string.IsNullOrEmpty(user.Bio) ? 1 : 0) * 0.20
            + (!string.IsNullOrEmpty(user.City) ? 1 : 0) * 0.15
            + (hasListedItems ? 1 : 0) * 0.20
            + (user.HasReturnPolicy ? 1 : 0) * 0.15;

        double accountAgeDays = (DateTime.UtcNow - user.CreatedAt).TotalDays;
        double accountMaturityScore = 1.0 / (1.0 + Math.Exp(-(accountAgeDays - MATURITY_CENTER_DAYS) / MATURITY_SCALE));

        // Generate improvement tips
        var tips = new List<string>();
        if (!user.IsGovernmentIdVerified)
            tips.Add("Verify your government ID to significantly boost your trust score.");
        if (!user.IsPhoneVerified)
            tips.Add("Add and verify your phone number.");
        if (string.IsNullOrEmpty(user.ProfilePictureUrl))
            tips.Add("Upload a profile photo.");
        if (string.IsNullOrEmpty(user.Bio))
            tips.Add("Write a short bio to build trust with renters.");
        if (!user.HasReturnPolicy)
            tips.Add("Define a return policy for your items.");
        if (completedRentals < 5)
            tips.Add("Complete more rentals to improve your activity score.");
        if (responsivenessScore < 0.7)
            tips.Add("Respond faster to rental requests to improve your responsiveness score.");

        return new ProfileTrustBreakdown
        {
            TotalScore = user.ProfileTrustScore,
            VerificationScore = Math.Round(verificationScore, 3),
            ReputationScore = Math.Round(reputationScore, 3),
            ActivityScore = Math.Round(activityScore, 3),
            ResponsivenessScore = Math.Round(responsivenessScore, 3),
            ProfileCompletenessScore = Math.Round(profileCompletenessScore, 3),
            AccountMaturityScore = Math.Round(accountMaturityScore, 3),
            DisputePenalty = Math.Round(disputePenalty, 3),
            ImprovementTips = tips,
        };
    }

    /// <inheritdoc/>
    public async Task<ItemScoreBreakdown> GetItemScoreBreakdownAsync(int itemId)
    {
        var item = await _context.Items
            .AsNoTracking()
            .Include(i => i.Images)
            .Include(i => i.Reviews.Where(r => !r.IsDeleted))
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null) return new ItemScoreBreakdown();

        double ratingScore = ComputeTimeWeightedBayesianItemRating(item);
        double ownerTrust = (item.User?.ProfileTrustScore ?? 0) / 100.0;
        double contentScore = ComputeContentQualityScore(item);

        int photoCount = item.Images?.Count ?? 0;
        if (photoCount == 0 && !string.IsNullOrEmpty(item.ImageUrl)) photoCount = 1;
        double photoScore = 1.0 - Math.Exp(-0.5 * photoCount);

        double pricingScore = await ComputePricingScoreAsync(item);

        int completedRentals = await _context.Rentals
            .CountAsync(r => r.ItemId == item.Id && r.Status == RentalStatus.Completed);
        double demandScore = Math.Min(
            Math.Log(1 + completedRentals + 0.5 * item.ViewsLast30Days / 100.0) / Math.Log(1 + DEMAND_REF),
            1.0);

        double daysSinceActivity = (DateTime.UtcNow - item.LastActivityDate).TotalDays;
        double freshnessScore = Math.Exp(-Math.Log(2) * daysSinceActivity / FRESHNESS_HALF_LIFE);

        double daysSinceListed = (DateTime.UtcNow - item.CreatedAt).TotalDays;
        double boostMultiplier = 1.0;
        if (daysSinceListed <= BOOST_WINDOW_DAYS)
        {
            double decay = Math.Exp(-Math.Log(2) * daysSinceListed / BOOST_HALF_LIFE);
            boostMultiplier = 1.0 + BOOST_MAX * decay;
        }

        double seasonalBoost = ComputeSeasonalBoost(item.Category);

        // Tips
        var tips = new List<string>();
        if (photoCount < 3)
            tips.Add("Add more photos (3–5 recommended) to improve your photo score.");
        if ((item.Description?.Length ?? 0) < 300)
            tips.Add("Write a longer, more detailed description (300+ characters ideal).");
        if ((item.Title?.Length ?? 0) < 30)
            tips.Add("Make your title more descriptive (aim for 30–60 characters).");
        if (string.IsNullOrEmpty(item.Condition))
            tips.Add("Specify the item condition (New, Like New, Good, etc.).");
        if (!item.HasAvailability)
            tips.Add("Set your availability calendar to improve ranking.");
        if (!item.DepositAmount.HasValue || item.DepositAmount <= 0)
            tips.Add("Setting a deposit amount shows renters you care about your items.");

        return new ItemScoreBreakdown
        {
            TotalScore = item.ItemScore,
            ItemRatingScore = Math.Round(ratingScore, 3),
            OwnerTrustComponent = Math.Round(ownerTrust, 3),
            ContentQualityScore = Math.Round(contentScore, 3),
            PhotoScore = Math.Round(photoScore, 3),
            PricingScore = Math.Round(pricingScore, 3),
            DemandScore = Math.Round(demandScore, 3),
            FreshnessScore = Math.Round(freshnessScore, 3),
            NewListingBoostMultiplier = Math.Round(boostMultiplier, 3),
            SeasonalBoost = Math.Round(seasonalBoost, 3),
            ImprovementTips = tips,
        };
    }

    #endregion

    // ================================================================
    //  PUBLIC API — Anti-Gaming
    // ================================================================

    #region Anti-Gaming

    /// <inheritdoc/>
    public async Task<bool> DetectReviewVelocityAnomalyAsync(string userId)
    {
        // Historical weekly review rate (last 90 days)
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        int reviewsLast90 = await _context.Reviews
            .CountAsync(r => !r.IsDeleted
                          && r.Item != null && r.Item.UserId == userId
                          && r.CreatedAt >= ninetyDaysAgo);

        double weeklyRate = reviewsLast90 / (90.0 / 7.0);

        // Recent spike (last 7 days)
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        int reviewsLastWeek = await _context.Reviews
            .CountAsync(r => !r.IsDeleted
                          && r.Item != null && r.Item.UserId == userId
                          && r.CreatedAt >= sevenDaysAgo);

        if (weeklyRate < 1) weeklyRate = 1; // floor to avoid division by zero for new users

        bool isAnomaly = reviewsLastWeek > VELOCITY_MULTIPLIER_THRESHOLD * weeklyRate;

        if (isAnomaly)
        {
            _logger.LogWarning(
                "Review velocity anomaly detected for user {UserId}: {Recent} reviews in 7 days vs {Historical:F1}/week historical rate",
                userId, reviewsLastWeek, weeklyRate);
        }

        return isAnomaly;
    }

    #endregion

    // ================================================================
    //  PUBLIC API — Personalization
    // ================================================================

    #region Personalization

    /// <inheritdoc/>
    public async Task RecordCategoryInteractionAsync(string userId, string category)
    {
        if (string.IsNullOrEmpty(category)) return;

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;

        var affinities = DeserializeCategoryAffinity(user.CategoryAffinityJson);
        affinities.TryGetValue(category, out int current);
        affinities[category] = current + 1;

        string json = JsonSerializer.Serialize(affinities);
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.CategoryAffinityJson, json));
    }

    /// <inheritdoc/>
    public async Task<double> GetPersonalizedBoostAsync(string? userId, string? itemCategory)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemCategory))
            return 0;

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CategoryAffinityJson)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(user)) return 0;

        var affinities = DeserializeCategoryAffinity(user);
        if (!affinities.TryGetValue(itemCategory, out int count)) return 0;

        int totalInteractions = affinities.Values.Sum();
        if (totalInteractions == 0) return 0;

        double affinityScore = (double)count / totalInteractions;
        return PERSONALIZATION_BOOST_MAX * affinityScore;
    }

    private static Dictionary<string, int> DeserializeCategoryAffinity(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, int>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
        }
        catch
        {
            return new Dictionary<string, int>();
        }
    }

    #endregion

    // ================================================================
    //  HELPERS
    // ================================================================

    #region Helpers

    /// <summary>Category → median price cache (refreshed per batch run).</summary>
    private Dictionary<string, decimal>? _categoryMedians;

    private async Task RefreshCategoryMediansAsync()
    {
        _categoryMedians = await _context.Items
            .AsNoTracking()
            .Where(i => i.IsListed && !i.IsAdminHidden && i.Price != null && i.Category != null)
            .GroupBy(i => i.Category!)
            .Where(g => g.Count() >= 5) // need at least 5 items for meaningful median
            .Select(g => new { Category = g.Key, Prices = g.Select(i => i.Price!.Value).ToList() })
            .ToDictionaryAsync(
                x => x.Category,
                x => ComputeMedian(x.Prices));
    }

    private async Task<decimal?> GetCategoryMedianPriceAsync(string? category)
    {
        if (string.IsNullOrEmpty(category)) return null;

        // Use cached value if available
        if (_categoryMedians != null)
        {
            return _categoryMedians.TryGetValue(category, out var cached) ? cached : null;
        }

        // Fallback: compute on the fly
        var prices = await _context.Items
            .AsNoTracking()
            .Where(i => i.IsListed && !i.IsAdminHidden && i.Price != null && i.Category == category)
            .Select(i => i.Price!.Value)
            .ToListAsync();

        return prices.Count >= 5 ? ComputeMedian(prices) : null;
    }

    private static decimal ComputeMedian(List<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        if (n == 0) return 0;
        return n % 2 == 1
            ? sorted[n / 2]
            : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }

    #endregion

    // ================================================================
    //  STATIC UTILITY — Geo-distance (Haversine)
    // ================================================================

    #region Geo

    /// <summary>
    /// Compute DistanceScore between user and item location (§11.2).
    /// Returns e^(-0.1 × distanceKm), or 1.0 if no geo data.
    /// </summary>
    public static double ComputeDistanceScore(
        double? userLat, double? userLon,
        double? itemLat, double? itemLon)
    {
        if (userLat == null || userLon == null || itemLat == null || itemLon == null)
            return 1.0; // no geo data — neutral

        double distKm = HaversineDistanceKm(userLat.Value, userLon.Value, itemLat.Value, itemLon.Value);
        return Math.Exp(-0.1 * distKm);
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in km
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                   * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    #endregion

    // ================================================================
    //  STATIC UTILITY — Sponsored Ad Ranking (§7)
    // ================================================================

    #region Sponsored

    /// <summary>
    /// Returns AdRank = BidAmount × ItemScore (§7.2).
    /// Returns null if the item is ineligible for promotion.
    /// </summary>
    public static double? ComputeAdRank(Item item)
    {
        if (!item.IsSponsored
            || item.SponsoredBidAmount == null
            || item.SponsoredBidAmount <= 0
            || item.SponsoredUntil == null
            || item.SponsoredUntil < DateTime.UtcNow)
            return null;

        if (item.ItemScore < MIN_ITEM_SCORE_FOR_SPONSORED)
            return null;

        return (double)item.SponsoredBidAmount.Value * item.ItemScore;
    }

    /// <summary>
    /// Interleave sponsored items into organic results according to §7.1:
    /// Positions 1–3 organic, then 1 sponsored per 4–5 organic.
    /// Applies diversity constraint (§11.8): max 3 items per owner in top 20.
    /// </summary>
    public static List<(Item Item, bool IsSponsored)> InterleaveResults(
        List<Item> organicItems,
        List<Item> sponsoredItems)
    {
        var result = new List<(Item Item, bool IsSponsored)>();
        var ownerCount = new Dictionary<string, int>();

        int organicIdx = 0;
        int sponsoredIdx = 0;
        int positionInResult = 0;

        // Sponsored insertion positions: 4, 9, 14, 19, ...
        var sponsoredPositions = new HashSet<int> { 3, 8, 13, 18, 23, 28 }; // 0-indexed

        while (organicIdx < organicItems.Count || sponsoredIdx < sponsoredItems.Count)
        {
            // Try to insert sponsored item at designated position
            if (sponsoredPositions.Contains(positionInResult) && sponsoredIdx < sponsoredItems.Count)
            {
                var sponsored = sponsoredItems[sponsoredIdx];
                sponsoredIdx++;
                result.Add((sponsored, true));
                positionInResult++;
                continue;
            }

            // Insert organic item with diversity constraint
            if (organicIdx < organicItems.Count)
            {
                var item = organicItems[organicIdx];
                organicIdx++;

                string ownerId = item.UserId ?? "";
                ownerCount.TryGetValue(ownerId, out int cnt);

                // §11.8 Diversity: skip if owner already has MAX_OWNER_ITEMS_TOP20 in top 20
                if (positionInResult < 20 && cnt >= MAX_OWNER_ITEMS_TOP20)
                {
                    // Don't increment positionInResult, try next item
                    continue;
                }

                ownerCount[ownerId] = cnt + 1;
                result.Add((item, false));
                positionInResult++;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    #endregion
}
