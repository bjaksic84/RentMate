using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

public class ScoringServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly SqliteConnection _connection;
    private readonly ScoringService _sut;

    public ScoringServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqlite();
        _sut = new ScoringService(_context, Mock.Of<ILogger<ScoringService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // ================================================================
    //  Profile Trust Score
    // ================================================================

    #region Profile Trust Score

    [Fact]
    public async Task ProfileTrust_FullyVerified_HighScore()
    {
        var user = EntityFactory.CreateUser(
            emailConfirmed: true,
            isPhoneVerified: true,
            isGovernmentIdVerified: true,
            isSocialMediaLinked: true,
            hasPaymentMethodAdded: true,
            profilePictureUrl: "https://pic.jpg",
            bio: "Experienced renter",
            city: "Ljubljana",
            hasReturnPolicy: true,
            responseRate: 95,
            avgResponseTimeHours: 2,
            totalMessagesReceived: 50,
            createdAt: DateTime.UtcNow.AddDays(-365));
        _context.Users.Add(user);

        // Add completed rentals as owner
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);
        var item = EntityFactory.CreateItem(userId: user.Id, isListed: true);
        _context.Items.Add(item);
        for (int i = 0; i < 10; i++)
        {
            var rental = EntityFactory.CreateRental(
                itemId: item.Id, renterId: renter.Id, ownerId: user.Id,
                status: RentalStatus.Completed);
            _context.Rentals.Add(rental);
        }

        // Add positive reviews
        for (int i = 0; i < 5; i++)
        {
            _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: renter.Id, rating: 5));
        }
        await _context.SaveChangesAsync();

        var score = await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);

        Assert.True(score > 70, $"Expected > 70, got {score:F2}");
    }

    [Fact]
    public async Task ProfileTrust_EmptyUser_LowScore()
    {
        var user = EntityFactory.CreateUser(
            emailConfirmed: false,
            isPhoneVerified: false,
            isGovernmentIdVerified: false,
            profilePictureUrl: null,
            bio: null,
            city: null,
            createdAt: DateTime.UtcNow);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var score = await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);

        Assert.True(score < 30, $"Expected < 30, got {score:F2}");
    }

    [Fact]
    public async Task ProfileTrust_NotFound_ReturnsZero()
    {
        var score = await _sut.ComputeAndSaveProfileTrustScoreAsync("nonexistent-user");
        Assert.Equal(0, score);
    }

    [Fact]
    public async Task ProfileTrust_PersistsToDb()
    {
        var user = EntityFactory.CreateUser(emailConfirmed: true, createdAt: DateTime.UtcNow.AddDays(-100));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);

        // Re-read from DB (ExecuteUpdateAsync bypasses change tracker)
        var loaded = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.True(loaded.ProfileTrustScore > 0);
        Assert.NotNull(loaded.ProfileTrustScoreUpdatedAt);
    }

    [Fact]
    public async Task ProfileTrust_EmailOnlyWeight()
    {
        var user = EntityFactory.CreateUser(
            emailConfirmed: true,
            isPhoneVerified: false,
            isGovernmentIdVerified: false,
            profilePictureUrl: null,
            createdAt: DateTime.UtcNow.AddDays(-100));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var withEmail = await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);

        // Email contributes 0.15 to verification, which is 15% of total
        // Score should be small but positive
        Assert.True(withEmail > 0, "Email verification should contribute to score");
    }

    [Fact]
    public async Task ProfileTrust_Responsiveness_NeutralForNew()
    {
        var user = EntityFactory.CreateUser(
            totalMessagesReceived: 0,
            createdAt: DateTime.UtcNow.AddDays(-100));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // TotalMessagesReceived=0 → responsiveness score = 0.5 (neutral)
        // Just verify it doesn't crash and returns a reasonable score
        var score = await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);
        Assert.True(score >= 0);
    }

    [Fact]
    public async Task ProfileTrust_Maturity_Young()
    {
        var user = EntityFactory.CreateUser(createdAt: DateTime.UtcNow);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var young = await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);

        // Also test an old account
        var oldUser = EntityFactory.CreateUser(createdAt: DateTime.UtcNow.AddDays(-365));
        _context.Users.Add(oldUser);
        await _context.SaveChangesAsync();

        var old = await _sut.ComputeAndSaveProfileTrustScoreAsync(oldUser.Id);

        Assert.True(old > young, $"Old account ({old:F2}) should score higher than young ({young:F2})");
    }

    [Fact]
    public async Task ProfileTrust_DisputePenalty_ReducesScore()
    {
        // User without disputes
        var user1 = EntityFactory.CreateUser(emailConfirmed: true, createdAt: DateTime.UtcNow.AddDays(-200));
        _context.Users.Add(user1);

        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);
        var item1 = EntityFactory.CreateItem(userId: user1.Id, isListed: true);
        _context.Items.Add(item1);

        // Completed rental + review for user1
        var rental1 = EntityFactory.CreateRental(itemId: item1.Id, renterId: renter.Id, ownerId: user1.Id, status: RentalStatus.Completed);
        _context.Rentals.Add(rental1);
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item1.Id, reviewerId: renter.Id, rating: 5));
        await _context.SaveChangesAsync();

        var scoreNoPenalty = await _sut.ComputeAndSaveProfileTrustScoreAsync(user1.Id);

        // User with admin-ruled-against dispute
        var user2 = EntityFactory.CreateUser(emailConfirmed: true, createdAt: DateTime.UtcNow.AddDays(-200));
        _context.Users.Add(user2);
        var item2 = EntityFactory.CreateItem(userId: user2.Id, isListed: true);
        _context.Items.Add(item2);
        var rental2 = EntityFactory.CreateRental(itemId: item2.Id, renterId: renter.Id, ownerId: user2.Id, status: RentalStatus.Completed);
        _context.Rentals.Add(rental2);
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item2.Id, reviewerId: renter.Id, rating: 5));

        // Deposit dispute where admin ruled against owner (released after admin resolve)
        var deposit = EntityFactory.CreateDeposit(
            rentalId: rental2.Id, amount: 100m, status: DepositStatus.Released);
        deposit.DisputedAt = DateTime.UtcNow.AddDays(-10);
        deposit.AdminResolvedAt = DateTime.UtcNow.AddDays(-5);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        var scoreWithPenalty = await _sut.ComputeAndSaveProfileTrustScoreAsync(user2.Id);

        Assert.True(scoreWithPenalty < scoreNoPenalty,
            $"Score with dispute ({scoreWithPenalty:F2}) should be less than without ({scoreNoPenalty:F2})");
    }

    #endregion

    // ================================================================
    //  Global Mean
    // ================================================================

    #region RecalculateGlobalMean

    [Fact]
    public async Task RecalculateGlobalMean_WithReviews()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        var item = EntityFactory.CreateItem(userId: user.Id);
        _context.Items.Add(item);

        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 3));
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 4));
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 5));
        await _context.SaveChangesAsync();

        await _sut.RecalculateGlobalMeanAsync();

        // Now compute a score — the global mean should affect Bayesian calculations
        // If it didn't crash, that's the main assertion. The mean should be 4.0.
        var score = await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);
        Assert.True(score >= 0);
    }

    [Fact]
    public async Task RecalculateGlobalMean_NoReviews_Default()
    {
        // No reviews in DB — should use default 3.5
        await _sut.RecalculateGlobalMeanAsync();

        // Verify it doesn't crash
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var score = await _sut.ComputeAndSaveProfileTrustScoreAsync(user.Id);
        Assert.True(score >= 0);
    }

    #endregion

    // ================================================================
    //  Velocity Anomaly Detection
    // ================================================================

    #region Velocity Anomaly

    [Fact]
    public async Task VelocityAnomaly_NormalRate_False()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        var item = EntityFactory.CreateItem(userId: user.Id);
        _context.Items.Add(item);

        // 3 reviews over 30 days — normal rate
        for (int i = 0; i < 3; i++)
        {
            _context.Reviews.Add(EntityFactory.CreateReview(
                itemId: item.Id, reviewerId: user.Id, rating: 4,
                createdAt: DateTime.UtcNow.AddDays(-i * 10)));
        }
        await _context.SaveChangesAsync();

        var result = await _sut.DetectReviewVelocityAnomalyAsync(user.Id);
        Assert.False(result);
    }

    [Fact]
    public async Task VelocityAnomaly_Spike_True()
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner");
        _context.Users.Add(owner);
        var item = EntityFactory.CreateItem(userId: owner.Id);
        _context.Items.Add(item);

        // Historical: 1 review per 2 weeks for past 3 months
        for (int i = 1; i <= 6; i++)
        {
            var reviewer = EntityFactory.CreateUser(firstName: $"OldReviewer{i}");
            _context.Users.Add(reviewer);
            _context.Reviews.Add(EntityFactory.CreateReview(
                itemId: item.Id, reviewerId: reviewer.Id, rating: 4,
                createdAt: DateTime.UtcNow.AddDays(-14 * i)));
        }

        // Spike: 10 reviews in last 7 days (> 3x historical rate)
        for (int i = 0; i < 10; i++)
        {
            var reviewer = EntityFactory.CreateUser(firstName: $"NewReviewer{i}");
            _context.Users.Add(reviewer);
            _context.Reviews.Add(EntityFactory.CreateReview(
                itemId: item.Id, reviewerId: reviewer.Id, rating: 5,
                createdAt: DateTime.UtcNow.AddDays(-i)));
        }
        await _context.SaveChangesAsync();

        var result = await _sut.DetectReviewVelocityAnomalyAsync(owner.Id);
        Assert.True(result);
    }

    #endregion

    // ================================================================
    //  Item Score
    // ================================================================

    #region Item Score

    [Fact]
    public async Task ItemScore_WellRated_HighScore()
    {
        var owner = EntityFactory.CreateUser(
            profileTrustScore: 80,
            createdAt: DateTime.UtcNow.AddDays(-365));
        _context.Users.Add(owner);

        var item = EntityFactory.CreateItem(
            userId: owner.Id,
            title: "High Quality Professional Camera for Events",
            description: string.Concat(Enumerable.Repeat("Great camera. ", 25)),
            condition: "Like New",
            category: "Electronics",
            depositAmount: 200m,
            isListed: true,
            viewsLast30Days: 50,
            createdAt: DateTime.UtcNow.AddDays(-30));
        item.User = owner;
        _context.Items.Add(item);

        // Add images
        for (int i = 0; i < 5; i++)
            _context.ItemImages.Add(EntityFactory.CreateItemImage(itemId: item.Id, displayOrder: i));

        // Add reviews
        var reviewer = EntityFactory.CreateUser(firstName: "Reviewer", profileTrustScore: 70);
        _context.Users.Add(reviewer);
        for (int i = 0; i < 5; i++)
        {
            var rental = EntityFactory.CreateRental(itemId: item.Id, renterId: reviewer.Id, ownerId: owner.Id, status: RentalStatus.Completed);
            _context.Rentals.Add(rental);
            _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: reviewer.Id, rating: 5));
        }
        await _context.SaveChangesAsync();

        var score = await _sut.ComputeAndSaveItemScoreAsync(item.Id);

        Assert.True(score > 0.5, $"Expected > 0.5, got {score:F4}");
    }

    [Fact]
    public async Task ItemScore_NotFound_ReturnsZero()
    {
        var score = await _sut.ComputeAndSaveItemScoreAsync(99999);
        Assert.Equal(0, score);
    }

    [Fact]
    public async Task ItemScore_PersistsToDb()
    {
        var owner = EntityFactory.CreateUser();
        _context.Users.Add(owner);
        var item = EntityFactory.CreateItem(userId: owner.Id);
        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        await _sut.ComputeAndSaveItemScoreAsync(item.Id);

        var loaded = await _context.Items.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        Assert.True(loaded.ItemScore > 0);
        Assert.NotNull(loaded.ItemScoreUpdatedAt);
    }

    [Fact]
    public async Task ItemScore_NewListingBoost_WithinWindow()
    {
        var owner = EntityFactory.CreateUser();
        _context.Users.Add(owner);
        var newItem = EntityFactory.CreateItem(userId: owner.Id, createdAt: DateTime.UtcNow.AddDays(-2));
        var oldItem = EntityFactory.CreateItem(userId: owner.Id, createdAt: DateTime.UtcNow.AddDays(-30));
        _context.Items.AddRange(newItem, oldItem);
        await _context.SaveChangesAsync();

        var newScore = await _sut.ComputeAndSaveItemScoreAsync(newItem.Id);
        var oldScore = await _sut.ComputeAndSaveItemScoreAsync(oldItem.Id);

        Assert.True(newScore > oldScore,
            $"New listing ({newScore:F4}) should score higher than old ({oldScore:F4}) due to boost");
    }

    [Fact]
    public async Task ItemScore_PhotoScore_Multiple()
    {
        var owner = EntityFactory.CreateUser();
        _context.Users.Add(owner);

        var itemWith = EntityFactory.CreateItem(userId: owner.Id);
        _context.Items.Add(itemWith);
        for (int i = 0; i < 5; i++)
            _context.ItemImages.Add(EntityFactory.CreateItemImage(itemId: itemWith.Id, displayOrder: i));

        var itemWithout = EntityFactory.CreateItem(userId: owner.Id);
        _context.Items.Add(itemWithout);

        await _context.SaveChangesAsync();

        var scoreWith = await _sut.ComputeAndSaveItemScoreAsync(itemWith.Id);
        var scoreWithout = await _sut.ComputeAndSaveItemScoreAsync(itemWithout.Id);

        Assert.True(scoreWith > scoreWithout,
            $"Item with photos ({scoreWith:F4}) should score higher than without ({scoreWithout:F4})");
    }

    [Fact]
    public async Task ItemScore_NoReviews_UsesGlobalMean()
    {
        var owner = EntityFactory.CreateUser();
        _context.Users.Add(owner);
        var item = EntityFactory.CreateItem(userId: owner.Id);
        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        var score = await _sut.ComputeAndSaveItemScoreAsync(item.Id);

        // With default global mean 3.5 and no reviews, the Bayesian rating is (3.5-1)/4 = 0.625
        // Score should be positive and reflect the global mean
        Assert.True(score > 0, "Score should be positive even without reviews");
    }

    [Fact]
    public async Task ItemScore_ContentQuality_Full()
    {
        var owner = EntityFactory.CreateUser();
        _context.Users.Add(owner);

        var goodItem = EntityFactory.CreateItem(
            userId: owner.Id,
            title: "Professional DSLR Camera Canon EOS R6 Full Frame",
            description: string.Concat(Enumerable.Repeat("High-quality rental. ", 20)),
            condition: "Like New",
            category: "Electronics",
            depositAmount: 100m);

        var bareItem = EntityFactory.CreateItem(
            userId: owner.Id,
            title: "Cam",
            description: "ok",
            condition: null,
            category: null);

        _context.Items.AddRange(goodItem, bareItem);
        await _context.SaveChangesAsync();

        var goodScore = await _sut.ComputeAndSaveItemScoreAsync(goodItem.Id);
        var bareScore = await _sut.ComputeAndSaveItemScoreAsync(bareItem.Id);

        Assert.True(goodScore > bareScore,
            $"Complete item ({goodScore:F4}) should score higher than bare ({bareScore:F4})");
    }

    [Fact]
    public async Task GetBreakdown_ReturnsComponents()
    {
        var user = EntityFactory.CreateUser(emailConfirmed: true, createdAt: DateTime.UtcNow.AddDays(-100));
        _context.Users.Add(user);
        var item = EntityFactory.CreateItem(userId: user.Id);
        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        var profileBreakdown = await _sut.GetProfileTrustBreakdownAsync(user.Id);
        Assert.True(profileBreakdown.TotalScore >= 0);

        var itemBreakdown = await _sut.GetItemScoreBreakdownAsync(item.Id);
        Assert.True(itemBreakdown.TotalScore >= 0);
    }

    #endregion
}
