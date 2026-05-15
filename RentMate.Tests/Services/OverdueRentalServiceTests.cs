using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RentMate.Hubs;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

public class OverdueRentalServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly Mock<IHubContext<RentMateHub>> _hubMock;
    private readonly Mock<IClientProxy> _clientProxy;
    private readonly OverdueRentalService _sut;

    public OverdueRentalServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _hubMock = new Mock<IHubContext<RentMateHub>>();
        _clientProxy = new Mock<IClientProxy>();

        // Setup hub mock chain: hubContext.Clients.User(id).SendAsync(...)
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxy.Object);
        _hubMock.Setup(h => h.Clients).Returns(hubClients.Object);

        // Mock IServiceScopeFactory to return our context + hub
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(RentMateContext))).Returns(_context);
        serviceProvider.Setup(sp => sp.GetService(typeof(IHubContext<RentMateHub>))).Returns(_hubMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _sut = new OverdueRentalService(scopeFactory.Object, Mock.Of<ILogger<OverdueRentalService>>());
    }

    public void Dispose() => _context.Dispose();

    // ================================================================
    //  CheckOverdueRentalsAsync
    // ================================================================

    [Fact]
    public async Task Overdue_ActivePastEndDate_SendsNotification()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(
            rentalStatus: RentalStatus.Active, depositAmount: null);
        rental.EndDate = DateTime.UtcNow.AddDays(-2); // Overdue
        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();

        await _sut.CheckOverdueRentalsAsync(CancellationToken.None);

        // Should send to both renter and owner
        _clientProxy.Verify(
            c => c.SendCoreAsync(RentMateHub.RentalOverdueEvent, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Overdue_ActiveFutureEndDate_NoNotification()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(
            rentalStatus: RentalStatus.Active, depositAmount: null);
        rental.EndDate = DateTime.UtcNow.AddDays(5); // Not overdue
        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();

        await _sut.CheckOverdueRentalsAsync(CancellationToken.None);

        _clientProxy.Verify(
            c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Overdue_CompletedPastEndDate_Skips()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(
            rentalStatus: RentalStatus.Completed, depositAmount: null);
        rental.EndDate = DateTime.UtcNow.AddDays(-2);
        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();

        await _sut.CheckOverdueRentalsAsync(CancellationToken.None);

        _clientProxy.Verify(
            c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ================================================================
    //  CheckDisputeDeadlinesAsync
    // ================================================================

    [Fact]
    public async Task Deadline_Charged_Expired_AutoAccepts()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(depositAmount: 100m);
        var deposit = EntityFactory.CreateDeposit(rental: rental, amount: 100m,
            status: DepositStatus.Charged);
        deposit.ChargedAmount = 100m;
        deposit.DisputeDeadline = DateTime.UtcNow.AddDays(-1); // Expired

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        await _sut.CheckDisputeDeadlinesAsync(CancellationToken.None);

        var loaded = await _context.RentalDeposits.FirstAsync(d => d.Id == deposit.Id);
        Assert.Null(loaded.DisputeDeadline);
        Assert.Equal(DepositStatus.Charged, loaded.Status); // Status unchanged
    }

    [Fact]
    public async Task Deadline_Disputed_Expired_AutoReleases()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(depositAmount: 100m);
        var deposit = EntityFactory.CreateDeposit(rental: rental, amount: 100m,
            status: DepositStatus.Disputed);
        deposit.ChargedAmount = 100m;
        deposit.DisputeDeadline = DateTime.UtcNow.AddDays(-1);

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        await _sut.CheckDisputeDeadlinesAsync(CancellationToken.None);

        var loaded = await _context.RentalDeposits.FirstAsync(d => d.Id == deposit.Id);
        Assert.Equal(DepositStatus.Released, loaded.Status);
        Assert.Null(loaded.ChargedAmount);
        Assert.Null(loaded.DisputeDeadline);
    }

    [Fact]
    public async Task Deadline_CounterOffer_Expired_AutoAccepts()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(depositAmount: 100m);
        var deposit = EntityFactory.CreateDeposit(rental: rental, amount: 100m,
            status: DepositStatus.CounterOffered);
        deposit.ChargedAmount = 100m;
        deposit.CounterOfferAmount = 60m;
        deposit.DisputeDeadline = DateTime.UtcNow.AddDays(-1);

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        await _sut.CheckDisputeDeadlinesAsync(CancellationToken.None);

        var loaded = await _context.RentalDeposits.FirstAsync(d => d.Id == deposit.Id);
        Assert.Equal(60m, loaded.ChargedAmount);
        Assert.Equal(DepositStatus.PartiallyCharged, loaded.Status); // 60 < 100
        Assert.Null(loaded.DisputeDeadline);
    }

    [Fact]
    public async Task Deadline_FutureDeadline_NoAction()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(depositAmount: 100m);
        var deposit = EntityFactory.CreateDeposit(rental: rental, amount: 100m,
            status: DepositStatus.Charged);
        deposit.ChargedAmount = 100m;
        deposit.DisputeDeadline = DateTime.UtcNow.AddDays(3); // Not expired

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        await _sut.CheckDisputeDeadlinesAsync(CancellationToken.None);

        var loaded = await _context.RentalDeposits.FirstAsync(d => d.Id == deposit.Id);
        Assert.NotNull(loaded.DisputeDeadline); // Unchanged
        Assert.Equal(DepositStatus.Charged, loaded.Status);
    }

    [Fact]
    public async Task Deadline_NoDeadline_NoAction()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(depositAmount: 100m);
        var deposit = EntityFactory.CreateDeposit(rental: rental, amount: 100m,
            status: DepositStatus.Charged);
        deposit.ChargedAmount = 100m;
        deposit.DisputeDeadline = null;

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        await _sut.CheckDisputeDeadlinesAsync(CancellationToken.None);

        // Should not be picked up — no expired deadline
        _clientProxy.Verify(
            c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Deadline_Escalated_ClearsStaleDeadline()
    {
        var (owner, renter, item, rental, _) = EntityFactory.CreateFullRentalSetup(depositAmount: 100m);
        var deposit = EntityFactory.CreateDeposit(rental: rental, amount: 100m,
            status: DepositStatus.Escalated);
        deposit.DisputeDeadline = DateTime.UtcNow.AddDays(-1); // Stale

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        await _sut.CheckDisputeDeadlinesAsync(CancellationToken.None);

        var loaded = await _context.RentalDeposits.FirstAsync(d => d.Id == deposit.Id);
        Assert.Null(loaded.DisputeDeadline);
        Assert.Equal(DepositStatus.Escalated, loaded.Status); // Status unchanged
    }
}
