using Microsoft.Extensions.Logging;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

public class DepositServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly Mock<IPaymentService> _paymentMock;
    private readonly Mock<IFileUploadService> _fileUploadMock;
    private readonly DepositService _sut;

    public DepositServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _paymentMock = new Mock<IPaymentService>();
        _fileUploadMock = new Mock<IFileUploadService>();

        // Default: all payment operations succeed
        _paymentMock.Setup(p => p.AuthorizeAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(PaymentResult.Succeeded("pay_ref_test"));
        _paymentMock.Setup(p => p.CaptureAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(PaymentResult.Succeeded("pay_ref_test"));
        _paymentMock.Setup(p => p.ReleaseAsync(It.IsAny<string>()))
            .ReturnsAsync(PaymentResult.Succeeded("pay_ref_test"));

        _fileUploadMock.Setup(f => f.UploadFileAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("https://test.com/evidence.jpg");

        _sut = new DepositService(_context, _paymentMock.Object, _fileUploadMock.Object,
            Mock.Of<ILogger<DepositService>>());
    }

    public void Dispose() => _context.Dispose();

    /// <summary>Seeds a full rental setup into the context and returns all entities.</summary>
    private async Task<(ApplicationUser Owner, ApplicationUser Renter, Item Item, Rental Rental, RentalDeposit? Deposit)>
        SeedRentalAsync(
            decimal depositAmount = 100m,
            DepositStatus? depositStatus = null,
            RentalStatus rentalStatus = RentalStatus.Active)
    {
        var (owner, renter, item, rental, deposit) = EntityFactory.CreateFullRentalSetup(
            depositAmount: depositAmount,
            rentalStatus: rentalStatus,
            depositStatus: depositStatus);

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        if (deposit != null) _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        // Detach all to force fresh loads from DB
        _context.ChangeTracker.Clear();

        return (owner, renter, item, rental, deposit);
    }

    #region CreateAndAuthorizeDepositAsync

    [Fact]
    public async Task CreateAndAuthorize_ValidRental_CreatesAuthorizedDeposit()
    {
        var (owner, renter, item, rental, _) = await SeedRentalAsync(depositStatus: null);

        var deposit = await _sut.CreateAndAuthorizeDepositAsync(rental.Id, 100m);

        Assert.Equal(DepositStatus.Authorized, deposit.Status);
        Assert.Equal("pay_ref_test", deposit.PaymentReference);
        Assert.NotNull(deposit.AuthorizedAt);
    }

    [Fact]
    public async Task CreateAndAuthorize_RentalNotFound_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAndAuthorizeDepositAsync(99999, 100m));
    }

    [Fact]
    public async Task CreateAndAuthorize_DuplicateDeposit_Throws()
    {
        var (_, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAndAuthorizeDepositAsync(rental.Id, 100m));
    }

    [Fact]
    public async Task CreateAndAuthorize_PaymentFails_Throws()
    {
        _paymentMock.Setup(p => p.AuthorizeAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(PaymentResult.Failed("Card declined"));

        var (_, _, _, rental, _) = await SeedRentalAsync(depositStatus: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAndAuthorizeDepositAsync(rental.Id, 100m));
        Assert.Contains("failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ReleaseDepositAsync

    [Fact]
    public async Task Release_Authorized_ReleasesAndCompletesRental()
    {
        var (owner, _, item, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        var result = await _sut.ReleaseDepositAsync(rental.Id, owner.Id);

        Assert.Equal(DepositStatus.Released, result.Status);
        Assert.NotNull(result.ReleasedAt);

        var loadedRental = await _context.Rentals.Include(r => r.Item).FirstAsync(r => r.Id == rental.Id);
        Assert.Equal(RentalStatus.Completed, loadedRental.Status);
        Assert.False(loadedRental.Item!.IsRented);
    }

    [Fact]
    public async Task Release_WrongUser_Throws()
    {
        var (_, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.ReleaseDepositAsync(rental.Id, renter.Id));
    }

    [Fact]
    public async Task Release_WrongStatus_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Released);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ReleaseDepositAsync(rental.Id, owner.Id));
    }

    #endregion

    #region ChargeDepositAsync

    [Fact]
    public async Task Charge_FullAmount_SetsCharged()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositAmount: 100m, depositStatus: DepositStatus.Authorized);

        var result = await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage to item", owner.Id);

        Assert.Equal(DepositStatus.Charged, result.Status);
        Assert.Equal(100m, result.ChargedAmount);
        Assert.NotNull(result.DisputeDeadline);
        Assert.True(result.DisputeDeadline!.Value > DateTime.UtcNow.AddDays(4));
    }

    [Fact]
    public async Task Charge_PartialAmount_SetsPartiallyCharged()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositAmount: 100m, depositStatus: DepositStatus.Authorized);

        var result = await _sut.ChargeDepositAsync(rental.Id, 50m, "Minor scratch", owner.Id);

        Assert.Equal(DepositStatus.PartiallyCharged, result.Status);
        Assert.Equal(50m, result.ChargedAmount);
    }

    [Fact]
    public async Task Charge_ExceedsDeposit_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositAmount: 100m, depositStatus: DepositStatus.Authorized);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ChargeDepositAsync(rental.Id, 150m, "Damage", owner.Id));
    }

    [Fact]
    public async Task Charge_EmptyReason_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositAmount: 100m, depositStatus: DepositStatus.Authorized);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ChargeDepositAsync(rental.Id, 50m, "   ", owner.Id));
    }

    #endregion

    #region DisputeDepositAsync

    [Fact]
    public async Task Dispute_Charged_SetsDisputed()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        // First charge it
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.DisputeDepositAsync(rental.Id, "No damage occurred", renter.Id);

        Assert.Equal(DepositStatus.Disputed, result.Status);
        Assert.Equal("No damage occurred", result.DisputeReason);
        Assert.NotNull(result.DisputedAt);
    }

    [Fact]
    public async Task Dispute_PartiallyCharged_SetsDisputed()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 50m, "Scratch", owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.DisputeDepositAsync(rental.Id, "Not my fault", renter.Id);

        Assert.Equal(DepositStatus.Disputed, result.Status);
    }

    [Fact]
    public async Task Dispute_ExpiredDeadline_Throws()
    {
        var (owner, renter, _, rental, deposit) = await SeedRentalAsync(depositStatus: DepositStatus.Charged);
        // Set deadline in the past
        var d = await _context.RentalDeposits.FirstAsync(x => x.Id == deposit!.Id);
        d.DisputeDeadline = DateTime.UtcNow.AddDays(-1);
        d.ChargedAmount = 100m;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DisputeDepositAsync(rental.Id, "Late dispute", renter.Id));
    }

    [Fact]
    public async Task Dispute_NoDeadline_Throws()
    {
        var (_, renter, _, rental, deposit) = await SeedRentalAsync(depositStatus: DepositStatus.Charged);
        var d = await _context.RentalDeposits.FirstAsync(x => x.Id == deposit!.Id);
        d.DisputeDeadline = null;
        d.ChargedAmount = 100m;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DisputeDepositAsync(rental.Id, "No window", renter.Id));
    }

    [Fact]
    public async Task Dispute_NotRenter_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.DisputeDepositAsync(rental.Id, "I'm the owner", owner.Id));
    }

    #endregion

    #region ReleaseDisputedDepositAsync

    [Fact]
    public async Task ReleaseDisputed_Disputed_Releases()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.ReleaseDisputedDepositAsync(rental.Id, owner.Id);

        Assert.Equal(DepositStatus.Released, result.Status);
        Assert.Null(result.ChargedAmount);
    }

    [Fact]
    public async Task ReleaseDisputed_WrongStatus_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ReleaseDisputedDepositAsync(rental.Id, owner.Id));
    }

    #endregion

    #region AcceptChargeAsync

    [Fact]
    public async Task AcceptCharge_Charged_ClearsDeadline()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.AcceptChargeAsync(rental.Id, renter.Id);

        Assert.Null(result.DisputeDeadline);
    }

    [Fact]
    public async Task AcceptCharge_WrongUser_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AcceptChargeAsync(rental.Id, owner.Id));
    }

    #endregion

    #region CounterOfferDepositAsync

    [Fact]
    public async Task CounterOffer_Disputed_SetsCounterOffered()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.CounterOfferDepositAsync(rental.Id, 50m, "Meet in the middle", owner.Id);

        Assert.Equal(DepositStatus.CounterOffered, result.Status);
        Assert.Equal(50m, result.CounterOfferAmount);
        Assert.NotNull(result.CounterOfferAt);
        Assert.NotNull(result.DisputeDeadline);
        Assert.True(result.DisputeDeadline!.Value > DateTime.UtcNow.AddDays(2));
    }

    [Fact]
    public async Task CounterOffer_AmountTooHigh_Throws()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CounterOfferDepositAsync(rental.Id, 100m, "Same amount", owner.Id));
    }

    [Fact]
    public async Task CounterOffer_AmountZero_Throws()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CounterOfferDepositAsync(rental.Id, 0m, "Free", owner.Id));
    }

    [Fact]
    public async Task CounterOffer_NotOwner_Throws()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CounterOfferDepositAsync(rental.Id, 50m, "Counter", renter.Id));
    }

    [Fact]
    public async Task CounterOffer_WrongStatus_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        // Status is Charged, not Disputed
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CounterOfferDepositAsync(rental.Id, 50m, "Counter", owner.Id));
    }

    #endregion

    #region AcceptCounterOfferAsync

    [Fact]
    public async Task AcceptCounter_Updates()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();
        await _sut.CounterOfferDepositAsync(rental.Id, 50m, "Half", owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.AcceptCounterOfferAsync(rental.Id, renter.Id);

        Assert.Equal(50m, result.ChargedAmount);
        Assert.Equal(DepositStatus.PartiallyCharged, result.Status); // 50 < 100
        Assert.Null(result.DisputeDeadline);
    }

    #endregion

    #region RejectCounterOfferAsync

    [Fact]
    public async Task RejectCounter_RestoresOriginal()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();
        await _sut.CounterOfferDepositAsync(rental.Id, 50m, "Half", owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.RejectCounterOfferAsync(rental.Id, renter.Id);

        Assert.Equal(DepositStatus.Charged, result.Status);
        Assert.NotNull(result.DisputeDeadline);
        Assert.True(result.DisputeDeadline!.Value > DateTime.UtcNow.AddDays(4));
    }

    #endregion

    #region EscalateDisputeAsync

    [Fact]
    public async Task Escalate_OwnerFromDisputed()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.EscalateDisputeAsync(rental.Id, owner.Id, "I have proof");

        Assert.Equal(DepositStatus.Escalated, result.Status);
        Assert.NotNull(result.EscalatedAt);
        Assert.Null(result.DisputeDeadline);
        Assert.Equal("I have proof", result.OwnerDisputeResponse);
    }

    [Fact]
    public async Task Escalate_RenterFromCounterOffered()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();
        await _sut.CounterOfferDepositAsync(rental.Id, 50m, "Half", owner.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.EscalateDisputeAsync(rental.Id, renter.Id, "Unfair counter");

        Assert.Equal(DepositStatus.Escalated, result.Status);
        Assert.Contains("Escalation:", result.DisputeReason!);
    }

    [Fact]
    public async Task Escalate_RenterFromDisputed()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        var result = await _sut.EscalateDisputeAsync(rental.Id, renter.Id);

        Assert.Equal(DepositStatus.Escalated, result.Status);
    }

    [Fact]
    public async Task Escalate_OwnerFromCounterOffered_Throws()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();
        await _sut.CounterOfferDepositAsync(rental.Id, 50m, "Half", owner.Id);
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.EscalateDisputeAsync(rental.Id, owner.Id));
    }

    [Fact]
    public async Task Escalate_UnrelatedUser_Throws()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.EscalateDisputeAsync(rental.Id, "random-user-id"));
    }

    [Fact]
    public async Task Escalate_FromCharged_ByOwner_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        // Owner tries to escalate from Charged (not Disputed)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.EscalateDisputeAsync(rental.Id, owner.Id));
    }

    #endregion

    #region AdminResolveDisputeAsync

    private async Task<(ApplicationUser Owner, ApplicationUser Renter, Rental Rental)> SeedEscalatedDisputeAsync()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();
        await _sut.EscalateDisputeAsync(rental.Id, owner.Id, "Proof attached");
        _context.ChangeTracker.Clear();
        return (owner, renter, rental);
    }

    [Fact]
    public async Task AdminResolve_ZeroAmount_Releases()
    {
        var (_, _, rental) = await SeedEscalatedDisputeAsync();

        var result = await _sut.AdminResolveDisputeAsync(rental.Id, 0m, "Renter is right", "admin-id");

        Assert.Equal(DepositStatus.Released, result.Status);
        Assert.Null(result.ChargedAmount);
        Assert.Equal("Renter is right", result.AdminNotes);
        Assert.NotNull(result.AdminResolvedAt);
    }

    [Fact]
    public async Task AdminResolve_FullAmount_Upholds()
    {
        var (_, _, rental) = await SeedEscalatedDisputeAsync();

        var result = await _sut.AdminResolveDisputeAsync(rental.Id, 100m, "Owner is right", "admin-id");

        Assert.Equal(DepositStatus.ChargeUpheld, result.Status);
        Assert.Equal(100m, result.ChargedAmount);
    }

    [Fact]
    public async Task AdminResolve_PartialAmount_Upholds()
    {
        var (_, _, rental) = await SeedEscalatedDisputeAsync();

        var result = await _sut.AdminResolveDisputeAsync(rental.Id, 60m, "Split the difference", "admin-id");

        Assert.Equal(DepositStatus.ChargeUpheld, result.Status);
        Assert.Equal(60m, result.ChargedAmount);
    }

    [Fact]
    public async Task AdminResolve_NotEscalated_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AdminResolveDisputeAsync(rental.Id, 50m, "Notes", "admin-id"));
    }

    [Fact]
    public async Task AdminResolve_NegativeAmount_Throws()
    {
        var (_, _, rental) = await SeedEscalatedDisputeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AdminResolveDisputeAsync(rental.Id, -10m, "Bad", "admin-id"));
    }

    [Fact]
    public async Task AdminResolve_ExceedsDeposit_Throws()
    {
        var (_, _, rental) = await SeedEscalatedDisputeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AdminResolveDisputeAsync(rental.Id, 200m, "Too much", "admin-id"));
    }

    #endregion

    #region UploadEvidenceAsync

    [Fact]
    public async Task UploadEvidence_Charged_OwnerUploads()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        var fileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);

        var evidence = await _sut.UploadEvidenceAsync(rental.Id, owner.Id, fileMock.Object, "Photo of damage");

        Assert.Equal("https://test.com/evidence.jpg", evidence.Url);
        Assert.Equal("Photo of damage", evidence.Notes);
        Assert.Equal(owner.Id, evidence.SubmittedByUserId);
    }

    [Fact]
    public async Task UploadEvidence_Disputed_RenterUploads()
    {
        var (owner, renter, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental.Id, "Not true", renter.Id);
        _context.ChangeTracker.Clear();

        var fileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);

        var evidence = await _sut.UploadEvidenceAsync(rental.Id, renter.Id, fileMock.Object, "Before photo");

        Assert.Equal(renter.Id, evidence.SubmittedByUserId);
    }

    [Fact]
    public async Task UploadEvidence_InvalidStatus_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        var fileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);

        // Status is Authorized — not valid for evidence
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UploadEvidenceAsync(rental.Id, owner.Id, fileMock.Object, "Evidence"));
    }

    [Fact]
    public async Task UploadEvidence_UnrelatedUser_Throws()
    {
        var (owner, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental.Id, 100m, "Damage", owner.Id);
        _context.ChangeTracker.Clear();

        var fileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UploadEvidenceAsync(rental.Id, "random-user", fileMock.Object, "Evidence"));
    }

    #endregion

    #region Query Methods

    [Fact]
    public async Task GetEscalated_ReturnsOnlyEscalated()
    {
        // Seed one escalated and one authorized
        await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        var (owner2, renter2, _, rental2, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);
        await _sut.ChargeDepositAsync(rental2.Id, 100m, "Damage", owner2.Id);
        _context.ChangeTracker.Clear();
        await _sut.DisputeDepositAsync(rental2.Id, "Not true", renter2.Id);
        _context.ChangeTracker.Clear();
        await _sut.EscalateDisputeAsync(rental2.Id, owner2.Id);
        _context.ChangeTracker.Clear();

        var escalated = await _sut.GetEscalatedDisputesAsync();

        Assert.Single(escalated);
        Assert.Equal(DepositStatus.Escalated, escalated[0].Status);
    }

    [Fact]
    public async Task GetResolved_ReturnsOnlyResolved()
    {
        var (_, _, rental) = await SeedEscalatedDisputeAsync();
        await _sut.AdminResolveDisputeAsync(rental.Id, 50m, "Split", "admin-id");
        _context.ChangeTracker.Clear();

        // Also seed an unresolved one
        await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        var resolved = await _sut.GetResolvedDisputesAsync();

        Assert.Single(resolved);
        Assert.NotNull(resolved[0].AdminResolvedAt);
    }

    [Fact]
    public async Task GetDepositSummary_CalculatesCorrectly()
    {
        var (owner, _, _, _, _) = await SeedRentalAsync(depositAmount: 100m, depositStatus: DepositStatus.Authorized);

        // Seed a second rental with Released deposit for same owner
        var renter2 = EntityFactory.CreateUser(firstName: "Renter2");
        var item2 = EntityFactory.CreateItem(userId: owner.Id, price: 15m);
        var rental2 = EntityFactory.CreateRental(item: item2, renter: renter2, ownerId: owner.Id);
        var deposit2 = EntityFactory.CreateDeposit(rental: rental2, amount: 200m, status: DepositStatus.Released);
        _context.Users.Add(renter2);
        _context.Items.Add(item2);
        _context.Rentals.Add(rental2);
        _context.RentalDeposits.Add(deposit2);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var summary = await _sut.GetDepositSummaryForOwnerAsync(owner.Id);

        Assert.Equal(100m, summary.TotalHeld);
        Assert.Equal(1, summary.ActiveDepositCount);
        Assert.Equal(200m, summary.TotalReleased);
    }

    [Fact]
    public async Task GetDepositSummary_EmptyForNewOwner()
    {
        var summary = await _sut.GetDepositSummaryForOwnerAsync("nonexistent-owner");

        Assert.Equal(0m, summary.TotalHeld);
        Assert.Equal(0, summary.ActiveDepositCount);
        Assert.Equal(0m, summary.TotalCharged);
        Assert.Equal(0m, summary.TotalReleased);
    }

    [Fact]
    public async Task GetDepositForRental_Exists_ReturnsDeposit()
    {
        var (_, _, _, rental, _) = await SeedRentalAsync(depositStatus: DepositStatus.Authorized);

        var result = await _sut.GetDepositForRentalAsync(rental.Id);

        Assert.NotNull(result);
        Assert.Equal(rental.Id, result!.RentalId);
    }

    [Fact]
    public async Task GetDepositForRental_NotExists_ReturnsNull()
    {
        var result = await _sut.GetDepositForRentalAsync(99999);

        Assert.Null(result);
    }

    #endregion
}
