using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RentMate.Tests.Helpers;

namespace RentMate.Tests.Services;

public class ReviewAggregationServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly SqliteConnection _connection;
    private readonly ReviewAggregationService _sut;

    public ReviewAggregationServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqlite();
        _sut = new ReviewAggregationService(_context, Mock.Of<ILogger<ReviewAggregationService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpdateAggregates_WithReviews_CorrectCountAndAverage()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        var item = EntityFactory.CreateItem(userId: user.Id);
        _context.Items.Add(item);
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 3));
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 4));
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 5));
        await _context.SaveChangesAsync();

        var result = await _sut.UpdateItemAggregatesAsync(item.Id);

        Assert.True(result);
        var loaded = await _context.Items.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        Assert.Equal(3, loaded.ReviewCount);
        Assert.Equal(4.0, loaded.AverageRating);
    }

    [Fact]
    public async Task UpdateAggregates_NoReviews_ResetsToDefaults()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        var item = EntityFactory.CreateItem(userId: user.Id);
        item.ReviewCount = 5;
        item.AverageRating = 4.5;
        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        await _sut.UpdateItemAggregatesAsync(item.Id);

        var loaded = await _context.Items.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        Assert.Equal(0, loaded.ReviewCount);
        Assert.Null(loaded.AverageRating);
    }

    [Fact]
    public async Task UpdateAggregates_SoftDeletedExcluded()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        var item = EntityFactory.CreateItem(userId: user.Id);
        _context.Items.Add(item);
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 5));
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 1, isDeleted: true));
        await _context.SaveChangesAsync();

        await _sut.UpdateItemAggregatesAsync(item.Id);

        var loaded = await _context.Items.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        Assert.Equal(1, loaded.ReviewCount);
        Assert.Equal(5.0, loaded.AverageRating);
    }

    [Fact]
    public async Task UpdateAggregates_ItemNotFound_ReturnsFalse()
    {
        var result = await _sut.UpdateItemAggregatesAsync(99999);
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAggregates_SingleReview()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        var item = EntityFactory.CreateItem(userId: user.Id);
        _context.Items.Add(item);
        _context.Reviews.Add(EntityFactory.CreateReview(itemId: item.Id, reviewerId: user.Id, rating: 3));
        await _context.SaveChangesAsync();

        await _sut.UpdateItemAggregatesAsync(item.Id);

        var loaded = await _context.Items.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        Assert.Equal(1, loaded.ReviewCount);
        Assert.Equal(3.0, loaded.AverageRating);
    }
}
