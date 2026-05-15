using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

public class CalendarServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly CalendarService _sut;

    public CalendarServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _sut = new CalendarService(_context);
    }

    public void Dispose() => _context.Dispose();

    private async Task<(ApplicationUser Owner, Item Item)> SeedItemAsync()
    {
        var owner = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: owner.Id);
        _context.Users.Add(owner);
        _context.Items.Add(item);
        await _context.SaveChangesAsync();
        return (owner, item);
    }

    [Fact]
    public async Task GetBlocked_ReturnsActiveAndPendingOnly()
    {
        var (owner, item) = await SeedItemAsync();
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);

        // Active rental (should be returned)
        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active,
            startDate: DateTime.Today, endDate: DateTime.Today.AddDays(5)));

        // Pending rental (should be returned)
        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Pending,
            startDate: DateTime.Today.AddDays(10), endDate: DateTime.Today.AddDays(15)));

        // Completed rental (should NOT be returned)
        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Completed,
            startDate: DateTime.Today.AddDays(20), endDate: DateTime.Today.AddDays(25)));

        await _context.SaveChangesAsync();

        var blocked = await _sut.GetBlockedDateRangesAsync(item.Id);

        Assert.Equal(2, blocked.Count);
    }

    [Fact]
    public async Task GetBlocked_ExcludesPastRentals()
    {
        var (owner, item) = await SeedItemAsync();
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);

        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active,
            startDate: DateTime.Today.AddDays(-20), endDate: DateTime.Today.AddDays(-10)));

        await _context.SaveChangesAsync();

        var blocked = await _sut.GetBlockedDateRangesAsync(item.Id);

        Assert.Empty(blocked);
    }

    [Fact]
    public async Task IsAvailable_NoConflict_ReturnsTrue()
    {
        var (_, item) = await SeedItemAsync();

        var result = await _sut.IsDateRangeAvailableAsync(
            item.Id,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)));

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailable_Overlap_ReturnsFalse()
    {
        var (owner, item) = await SeedItemAsync();
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);

        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active,
            startDate: DateTime.Today.AddDays(3), endDate: DateTime.Today.AddDays(10)));
        await _context.SaveChangesAsync();

        var result = await _sut.IsDateRangeAvailableAsync(
            item.Id,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)));

        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailable_ExcludeRentalId_IgnoresIt()
    {
        var (owner, item) = await SeedItemAsync();
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);

        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active,
            startDate: DateTime.Today.AddDays(1), endDate: DateTime.Today.AddDays(10));
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();

        var result = await _sut.IsDateRangeAvailableAsync(
            item.Id,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            excludeRentalId: rental.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailable_AdjacentDates_NoConflict()
    {
        var (owner, item) = await SeedItemAsync();
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);

        // Rental ends Jan 10
        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active,
            startDate: new DateTime(2026, 1, 5), endDate: new DateTime(2026, 1, 10)));
        await _context.SaveChangesAsync();

        // Request starts Jan 11 — no overlap
        var result = await _sut.IsDateRangeAvailableAsync(
            item.Id,
            new DateOnly(2026, 1, 11),
            new DateOnly(2026, 1, 15));

        Assert.True(result);
    }

    [Fact]
    public async Task GetAvailabilities_MultipleItems()
    {
        var (owner, item1) = await SeedItemAsync();
        var item2 = EntityFactory.CreateItem(userId: owner.Id);
        _context.Items.Add(item2);

        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);

        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item1.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active,
            startDate: DateTime.Today, endDate: DateTime.Today.AddDays(5)));
        await _context.SaveChangesAsync();

        var results = new List<ItemAvailability>();
        await foreach (var avail in _sut.GetItemAvailabilitiesAsync(new[] { item1.Id, item2.Id }))
        {
            results.Add(avail);
        }

        Assert.Equal(2, results.Count);
        Assert.Single(results.First(r => r.ItemId == item1.Id).BlockedRanges);
        Assert.Empty(results.First(r => r.ItemId == item2.Id).BlockedRanges);
    }
}
