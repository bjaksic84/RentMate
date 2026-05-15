using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

/// <summary>
/// DashboardService is the single source of truth for dashboard data
/// (multi-join aggregation + a 15-minute admin-stats cache). Counts,
/// renter/owner partitioning and cache invalidation are verified here.
/// </summary>
public class DashboardServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _userManager = MockUserManager.Create();
        _userManager.Setup(m => m.Users).Returns(() => _context.Users);
        _sut = new DashboardService(
            _context, _userManager.Object, _cache, Mock.Of<ILogger<DashboardService>>());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetUserDashboard_NullUserId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetUserDashboardAsync(""));
    }

    [Fact]
    public async Task GetUserDashboard_ComputesCountsAndPartitions()
    {
        var user = EntityFactory.CreateUser();
        var owner = EntityFactory.CreateUser();
        var listed = EntityFactory.CreateItem(userId: user.Id, isListed: true);
        var unlisted = EntityFactory.CreateItem(userId: user.Id, isListed: false);
        var othersItem = EntityFactory.CreateItem(userId: owner.Id, isListed: true);

        var asRenterActive = EntityFactory.CreateRental(
            itemId: othersItem.Id, renterId: user.Id, ownerId: owner.Id, status: RentalStatus.Active);
        var asRenterDone = EntityFactory.CreateRental(
            itemId: othersItem.Id, renterId: user.Id, ownerId: owner.Id, status: RentalStatus.Completed);
        var asOwnerActive = EntityFactory.CreateRental(
            itemId: listed.Id, renterId: owner.Id, ownerId: user.Id, status: RentalStatus.Active);

        _context.Users.AddRange(user, owner);
        _context.Items.AddRange(listed, unlisted, othersItem);
        _context.Rentals.AddRange(asRenterActive, asRenterDone, asOwnerActive);
        _context.Payments.Add(new Payment
        {
            RentalId = asRenterActive.Id, UserId = user.Id, Amount = 25m, Status = PaymentStatus.Success
        });
        await _context.SaveChangesAsync();

        var dash = await _sut.GetUserDashboardAsync(user.Id);

        Assert.Equal(2, dash.TotalListingsOwned);
        Assert.Equal(1, dash.ActiveListingsOwned);
        Assert.Equal(2, dash.TotalRentalsAsRenter);
        Assert.Equal(1, dash.ActiveRentalsAsRenter);
        Assert.Equal(1, dash.TotalRentalsAsOwner);
        Assert.Single(dash.RecentPayments);
    }

    [Fact]
    public async Task GetMyRentals_And_GetOwnerRentals_PartitionByRole()
    {
        var user = EntityFactory.CreateUser();
        var other = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: other.Id);
        var asRenter = EntityFactory.CreateRental(itemId: item.Id, renterId: user.Id, ownerId: other.Id);
        var asOwner = EntityFactory.CreateRental(itemId: item.Id, renterId: other.Id, ownerId: user.Id);

        _context.Users.AddRange(user, other);
        _context.Items.Add(item);
        _context.Rentals.AddRange(asRenter, asOwner);
        await _context.SaveChangesAsync();

        var mine = await _sut.GetMyRentalsAsync(user.Id);
        var owned = await _sut.GetOwnerRentalsAsync(user.Id);

        Assert.Single(mine);
        Assert.Equal(asRenter.Id, mine[0].Id);
        Assert.Single(owned);
        Assert.Equal(asOwner.Id, owned[0].Id);
    }

    [Fact]
    public async Task GetAdminDashboard_ComputesAggregateStats()
    {
        var u1 = EntityFactory.CreateUser();
        var u2 = EntityFactory.CreateUser();
        var listed = EntityFactory.CreateItem(userId: u1.Id, isListed: true);
        var unlisted = EntityFactory.CreateItem(userId: u1.Id, isListed: false);
        var rental = EntityFactory.CreateRental(itemId: listed.Id, renterId: u2.Id, ownerId: u1.Id, status: RentalStatus.Active);

        _context.Users.AddRange(u1, u2);
        _context.Items.AddRange(listed, unlisted);
        _context.Rentals.Add(rental);
        _context.Payments.AddRange(
            new Payment { RentalId = rental.Id, UserId = u2.Id, Amount = 100m, Status = PaymentStatus.Success },
            new Payment { RentalId = rental.Id, UserId = u2.Id, Amount = 50m, Status = PaymentStatus.Failed });
        await _context.SaveChangesAsync();

        var stats = await _sut.GetAdminDashboardAsync(useCache: false);

        Assert.Equal(2, stats.TotalUsers);
        Assert.Equal(2, stats.TotalListings);
        Assert.Equal(1, stats.ActiveListings);
        Assert.Equal(1, stats.ActiveRentals);
        Assert.Equal(100m, stats.TotalRevenue);
    }

    [Fact]
    public async Task InvalidateAdminCache_ForcesStatsRecompute()
    {
        var u1 = EntityFactory.CreateUser();
        _context.Users.Add(u1);
        _context.Items.Add(EntityFactory.CreateItem(userId: u1.Id, isListed: true));
        await _context.SaveChangesAsync();

        var first = await _sut.GetAdminDashboardAsync(useCache: true);
        Assert.Equal(1, first.TotalListings);

        _context.Items.Add(EntityFactory.CreateItem(userId: u1.Id, isListed: true));
        await _context.SaveChangesAsync();

        var cached = await _sut.GetAdminDashboardAsync(useCache: true);
        Assert.Equal(1, cached.TotalListings); // served from cache

        _sut.InvalidateAdminCache();
        var fresh = await _sut.GetAdminDashboardAsync(useCache: true);
        Assert.Equal(2, fresh.TotalListings); // recomputed
    }
}
