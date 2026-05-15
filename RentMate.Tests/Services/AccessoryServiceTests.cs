using Microsoft.Extensions.Logging;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

public class AccessoryServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly AccessoryService _sut;

    public AccessoryServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _sut = new AccessoryService(_context, Mock.Of<ILogger<AccessoryService>>());
    }

    public void Dispose() => _context.Dispose();

    private async Task<(ApplicationUser Owner, Item Item)> SeedItemAsync()
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner");
        var item = EntityFactory.CreateItem(userId: owner.Id);
        _context.Users.Add(owner);
        _context.Items.Add(item);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return (owner, item);
    }

    [Fact]
    public async Task GetAccessories_ReturnsAllForItem()
    {
        var (_, item) = await SeedItemAsync();
        _context.ItemAccessories.Add(EntityFactory.CreateAccessory(itemId: item.Id, name: "Bag"));
        _context.ItemAccessories.Add(EntityFactory.CreateAccessory(itemId: item.Id, name: "Charger"));
        await _context.SaveChangesAsync();

        var result = await _sut.GetAccessoriesForItemAsync(item.Id);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Add_ValidItem_CreatesAccessory()
    {
        var (_, item) = await SeedItemAsync();

        var result = await _sut.AddAccessoryAsync(item.Id, "Lens", 15m, "Wide angle");

        Assert.Equal("Lens", result.Name);
        Assert.Equal(15m, result.DailyPrice);
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task Add_InvalidItem_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddAccessoryAsync(99999, "Lens", 15m, null));
    }

    [Fact]
    public async Task Update_Owner_Success()
    {
        var (owner, item) = await SeedItemAsync();
        var acc = EntityFactory.CreateAccessory(itemId: item.Id, name: "OldName", dailyPrice: 5m);
        _context.ItemAccessories.Add(acc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _sut.UpdateAccessoryAsync(acc.Id, owner.Id, "NewName", 10m, false, "Updated");

        Assert.Equal("NewName", result.Name);
        Assert.Equal(10m, result.DailyPrice);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task Update_NotOwner_Throws()
    {
        var (_, item) = await SeedItemAsync();
        var acc = EntityFactory.CreateAccessory(itemId: item.Id);
        _context.ItemAccessories.Add(acc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateAccessoryAsync(acc.Id, "wrong-user", "Name", 10m, true, null));
    }

    [Fact]
    public async Task Update_NotFound_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAccessoryAsync(99999, "user", "Name", 10m, true, null));
    }

    [Fact]
    public async Task Delete_NoActiveRental_Deletes()
    {
        var (owner, item) = await SeedItemAsync();
        var acc = EntityFactory.CreateAccessory(itemId: item.Id);
        _context.ItemAccessories.Add(acc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _sut.DeleteAccessoryAsync(acc.Id, owner.Id);

        Assert.False(await _context.ItemAccessories.AnyAsync(a => a.Id == acc.Id));
    }

    [Fact]
    public async Task Delete_CompletedRentalAttached_Succeeds()
    {
        var (owner, item) = await SeedItemAsync();
        var acc = EntityFactory.CreateAccessory(itemId: item.Id);
        _context.ItemAccessories.Add(acc);

        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);
        var rental = EntityFactory.CreateRental(itemId: item.Id, renterId: renter.Id, ownerId: owner.Id, status: RentalStatus.Completed);
        _context.Rentals.Add(rental);

        var ra = new RentalAccessory { RentalId = rental.Id, ItemAccessoryId = acc.Id, Name = acc.Name, DailyPrice = acc.DailyPrice };
        _context.RentalAccessories.Add(ra);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _sut.DeleteAccessoryAsync(acc.Id, owner.Id);

        Assert.False(await _context.ItemAccessories.AnyAsync(a => a.Id == acc.Id));
    }

    [Fact]
    public async Task Delete_ActiveRentalAttached_Throws()
    {
        var (owner, item) = await SeedItemAsync();
        var acc = EntityFactory.CreateAccessory(itemId: item.Id);
        _context.ItemAccessories.Add(acc);

        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);
        var rental = EntityFactory.CreateRental(itemId: item.Id, renterId: renter.Id, ownerId: owner.Id, status: RentalStatus.Active);
        _context.Rentals.Add(rental);

        var ra = new RentalAccessory { RentalId = rental.Id, ItemAccessoryId = acc.Id, Name = acc.Name, DailyPrice = acc.DailyPrice };
        _context.RentalAccessories.Add(ra);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAccessoryAsync(acc.Id, owner.Id));
    }

    [Fact]
    public async Task Delete_NotOwner_Throws()
    {
        var (_, item) = await SeedItemAsync();
        var acc = EntityFactory.CreateAccessory(itemId: item.Id);
        _context.ItemAccessories.Add(acc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.DeleteAccessoryAsync(acc.Id, "wrong-user"));
    }

    [Fact]
    public async Task AttachToRental_SnapshotsPrice()
    {
        var (owner, item) = await SeedItemAsync();
        var acc1 = EntityFactory.CreateAccessory(itemId: item.Id, name: "Bag", dailyPrice: 5m);
        var acc2 = EntityFactory.CreateAccessory(itemId: item.Id, name: "Case", dailyPrice: 8m);
        _context.ItemAccessories.AddRange(acc1, acc2);

        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);
        var rental = EntityFactory.CreateRental(itemId: item.Id, renterId: renter.Id, ownerId: owner.Id);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _sut.AttachAccessoriesToRentalAsync(rental.Id, new List<int> { acc1.Id, acc2.Id });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Name == "Bag" && r.DailyPrice == 5m);
        Assert.Contains(result, r => r.Name == "Case" && r.DailyPrice == 8m);
    }

    [Fact]
    public async Task AttachToRental_UnavailableExcluded()
    {
        var (owner, item) = await SeedItemAsync();
        var available = EntityFactory.CreateAccessory(itemId: item.Id, name: "Bag", isAvailable: true);
        var unavailable = EntityFactory.CreateAccessory(itemId: item.Id, name: "Case", isAvailable: false);
        _context.ItemAccessories.AddRange(available, unavailable);

        var renter = EntityFactory.CreateUser(firstName: "Renter");
        _context.Users.Add(renter);
        var rental = EntityFactory.CreateRental(itemId: item.Id, renterId: renter.Id, ownerId: owner.Id);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _sut.AttachAccessoriesToRentalAsync(rental.Id, new List<int> { available.Id, unavailable.Id });

        Assert.Single(result);
        Assert.Equal("Bag", result[0].Name);
    }
}
