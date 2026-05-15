using Microsoft.Extensions.Logging;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

public class RentalExtensionServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly RentalExtensionService _sut;

    public RentalExtensionServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _sut = new RentalExtensionService(_context, Mock.Of<ILogger<RentalExtensionService>>());
    }

    public void Dispose() => _context.Dispose();

    /// <summary>Seeds an active rental with owner, renter, item. Returns all entities.</summary>
    private async Task<(ApplicationUser Owner, ApplicationUser Renter, Item Item, Rental Rental)>
        SeedActiveRentalAsync(
            decimal price = 15m,
            bool autoApprove = false,
            int? maxRentalDays = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner");
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        var item = EntityFactory.CreateItem(
            userId: owner.Id,
            price: price,
            autoApproveExtensions: autoApprove,
            maxRentalDays: maxRentalDays);

        var rental = EntityFactory.CreateRental(
            item: item,
            renter: renter,
            owner: owner,
            status: RentalStatus.Active,
            startDate: startDate ?? DateTime.UtcNow.AddDays(-5),
            endDate: endDate ?? DateTime.UtcNow.AddDays(5),
            totalPrice: price * 10);

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        return (owner, renter, item, rental);
    }

    // ================================================================
    //  RequestExtensionAsync
    // ================================================================

    #region Request

    [Fact]
    public async Task Request_Valid_CreatesPending()
    {
        var (_, renter, item, rental) = await SeedActiveRentalAsync(price: 15m);
        var newEnd = rental.EndDate.AddDays(5);

        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, newEnd);

        Assert.Equal(ExtensionStatus.Pending, ext.Status);
        Assert.Equal(15m, ext.DailyRate);
        Assert.Equal(75m, ext.AdditionalCost); // 15 * 5
    }

    [Fact]
    public async Task Request_AutoApprove_SetsAutoApproved()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync(autoApprove: true);
        var newEnd = rental.EndDate.AddDays(3);

        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, newEnd);

        Assert.Equal(ExtensionStatus.AutoApproved, ext.Status);
    }

    [Fact]
    public async Task Request_NotFound_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RequestExtensionAsync(99999, "user-id", DateTime.UtcNow.AddDays(10)));
    }

    [Fact]
    public async Task Request_InactiveRental_Throws()
    {
        var owner = EntityFactory.CreateUser(firstName: "Owner");
        var renter = EntityFactory.CreateUser(firstName: "Renter");
        var item = EntityFactory.CreateItem(userId: owner.Id);
        var rental = EntityFactory.CreateRental(item: item, renter: renter, owner: owner, status: RentalStatus.Completed);

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RequestExtensionAsync(rental.Id, renter.Id, DateTime.UtcNow.AddDays(10)));
    }

    [Fact]
    public async Task Request_NotRenter_Throws()
    {
        var (owner, _, _, rental) = await SeedActiveRentalAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RequestExtensionAsync(rental.Id, owner.Id, rental.EndDate.AddDays(3)));
    }

    [Fact]
    public async Task Request_DateBeforeEnd_Throws()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(-1)));
    }

    [Fact]
    public async Task Request_ExceedsMaxDays_Throws()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync(
            maxRentalDays: 10,
            startDate: DateTime.UtcNow.AddDays(-5),
            endDate: DateTime.UtcNow.AddDays(4));

        // Total would be 5+4+10 = 19 days, exceeding 10-day max
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(10)));
    }

    [Fact]
    public async Task Request_SchedulingConflict_Throws()
    {
        var (owner, renter, item, rental) = await SeedActiveRentalAsync();

        // Add a future rental for the same item (use itemId to avoid tracking conflict)
        var renter2 = EntityFactory.CreateUser(firstName: "FutureRenter");
        var futureRental = EntityFactory.CreateRental(
            itemId: item.Id,
            renter: renter2,
            ownerId: owner.Id,
            status: RentalStatus.Accepted,
            startDate: rental.EndDate.AddDays(1),
            endDate: rental.EndDate.AddDays(10));
        _context.Users.Add(renter2);
        _context.Rentals.Add(futureRental);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Try to extend past the future rental's start
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(5)));
    }

    [Fact]
    public async Task Request_PendingExists_Throws()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync();
        await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(5)));
    }

    [Fact]
    public async Task Request_CostCalculation()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync(price: 20m);
        var newEnd = rental.EndDate.AddDays(7);

        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, newEnd);

        Assert.Equal(20m, ext.DailyRate);
        Assert.Equal(140m, ext.AdditionalCost); // 20 * 7
    }

    #endregion

    // ================================================================
    //  ApproveExtensionAsync
    // ================================================================

    #region Approve

    [Fact]
    public async Task Approve_Pending_SetsAccepted()
    {
        var (owner, renter, _, rental) = await SeedActiveRentalAsync();
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();

        var result = await _sut.ApproveExtensionAsync(ext.Id, owner.Id);

        Assert.Equal(ExtensionStatus.Accepted, result.Status);
    }

    [Fact]
    public async Task Approve_NotPending_Throws()
    {
        var (owner, renter, _, rental) = await SeedActiveRentalAsync(autoApprove: true);
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();

        // Extension is AutoApproved, not Pending
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ApproveExtensionAsync(ext.Id, owner.Id));
    }

    [Fact]
    public async Task Approve_NotOwner_Throws()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync();
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.ApproveExtensionAsync(ext.Id, renter.Id));
    }

    [Fact]
    public async Task Approve_ConflictAppeared_Throws()
    {
        var (owner, renter, item, rental) = await SeedActiveRentalAsync();
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(5));
        _context.ChangeTracker.Clear();

        // New rental booked in the meantime (use itemId to avoid tracking conflict)
        var renter2 = EntityFactory.CreateUser(firstName: "FutureRenter");
        var conflict = EntityFactory.CreateRental(
            itemId: item.Id,
            renter: renter2,
            ownerId: owner.Id,
            status: RentalStatus.Accepted,
            startDate: rental.EndDate.AddDays(1),
            endDate: rental.EndDate.AddDays(10));
        _context.Users.Add(renter2);
        _context.Rentals.Add(conflict);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ApproveExtensionAsync(ext.Id, owner.Id));
    }

    #endregion

    // ================================================================
    //  DeclineExtensionAsync
    // ================================================================

    [Fact]
    public async Task Decline_Pending_SetsDeclined()
    {
        var (owner, renter, _, rental) = await SeedActiveRentalAsync();
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();

        var result = await _sut.DeclineExtensionAsync(ext.Id, owner.Id);

        Assert.Equal(ExtensionStatus.Declined, result.Status);
    }

    // ================================================================
    //  FinalizeExtensionAsync
    // ================================================================

    #region Finalize

    [Fact]
    public async Task Finalize_Accepted_UpdatesRental()
    {
        var (owner, renter, _, rental) = await SeedActiveRentalAsync(price: 10m);
        var originalTotal = rental.TotalPrice;
        var newEnd = rental.EndDate.AddDays(5);
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, newEnd);
        _context.ChangeTracker.Clear();
        await _sut.ApproveExtensionAsync(ext.Id, owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.FinalizeExtensionAsync(ext.Id, renter.Id);

        Assert.Equal(ExtensionStatus.Approved, result.Status);
        var loadedRental = await _context.Rentals.FirstAsync(r => r.Id == rental.Id);
        Assert.Equal(newEnd, loadedRental.EndDate);
        Assert.Equal(originalTotal + 50m, loadedRental.TotalPrice); // 10 * 5
    }

    [Fact]
    public async Task Finalize_AutoApproved_UpdatesRental()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync(price: 10m, autoApprove: true);
        var newEnd = rental.EndDate.AddDays(3);
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, newEnd);
        _context.ChangeTracker.Clear();

        var result = await _sut.FinalizeExtensionAsync(ext.Id, renter.Id);

        Assert.Equal(ExtensionStatus.Approved, result.Status);
    }

    [Fact]
    public async Task Finalize_NotAccepted_Throws()
    {
        var (_, renter, _, rental) = await SeedActiveRentalAsync();
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();

        // Extension is still Pending
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.FinalizeExtensionAsync(ext.Id, renter.Id));
    }

    #endregion

    // ================================================================
    //  CancelExtensionAsync
    // ================================================================

    [Fact]
    public async Task Cancel_Accepted_SetsDeclined()
    {
        var (owner, renter, _, rental) = await SeedActiveRentalAsync();
        var ext = await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();
        await _sut.ApproveExtensionAsync(ext.Id, owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.CancelExtensionAsync(ext.Id, renter.Id);

        Assert.Equal(ExtensionStatus.Declined, result.Status);
    }

    // ================================================================
    //  CanExtendAsync
    // ================================================================

    [Fact]
    public async Task CanExtend_NoConflicts_ReturnsTrue()
    {
        var (_, _, _, rental) = await SeedActiveRentalAsync();

        var result = await _sut.CanExtendAsync(rental.Id, rental.EndDate.AddDays(5));

        Assert.True(result);
    }

    // ================================================================
    //  Query Methods
    // ================================================================

    [Fact]
    public async Task GetPendingForOwner_ReturnsPendingOnly()
    {
        var (owner, renter, _, rental) = await SeedActiveRentalAsync();
        await _sut.RequestExtensionAsync(rental.Id, renter.Id, rental.EndDate.AddDays(3));
        _context.ChangeTracker.Clear();

        var pending = await _sut.GetPendingExtensionsForOwnerAsync(owner.Id);

        Assert.Single(pending);
        Assert.Equal(ExtensionStatus.Pending, pending[0].Status);
    }

    [Fact]
    public async Task GetEarliestConflict_ReturnsNextBooking()
    {
        var (owner, _, item, rental) = await SeedActiveRentalAsync();
        var renter2 = EntityFactory.CreateUser(firstName: "Future");
        _context.Users.Add(renter2);
        var futureStart = rental.EndDate.AddDays(5);
        _context.Rentals.Add(EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter2.Id, ownerId: owner.Id,
            status: RentalStatus.Accepted,
            startDate: futureStart, endDate: futureStart.AddDays(5)));
        await _context.SaveChangesAsync();

        var conflict = await _sut.GetEarliestConflictDateAsync(item.Id, rental.EndDate);

        Assert.NotNull(conflict);
        Assert.Equal(futureStart.Date, conflict!.Value.Date);
    }
}
