using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Tests.Services;

/// <summary>
/// AccountLifecycleService performs irreversible GDPR operations, so its
/// deactivation cascade and anonymisation must be verified directly.
/// SQLite is used because <c>DeleteAccountAsync</c> opens a real transaction.
/// </summary>
public class AccountLifecycleServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<IFileUploadService> _fileUpload = new();
    private readonly Mock<IPaymentService> _payment = new();
    private readonly AccountLifecycleService _sut;

    public AccountLifecycleServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqlite();
        _userManager = MockUserManager.Create();

        _fileUpload.Setup(f => f.DeleteFilesAsync(It.IsAny<IEnumerable<string>>())).Returns(Task.CompletedTask);
        _payment.Setup(p => p.DeleteCustomerAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _sut = new AccountLifecycleService(
            _context, _userManager.Object, _fileUpload.Object, _payment.Object,
            Mock.Of<ILogger<AccountLifecycleService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void TrackUserManagerFindById(ApplicationUser user) =>
        _userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);

    private async Task<ApplicationUser> SeedRenterWithRentalAsync(RentalStatus status)
    {
        var owner = EntityFactory.CreateUser();
        var renter = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: owner.Id);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id, status: status);
        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();
        return renter;
    }

    [Fact]
    public async Task HasActiveRentals_ActiveRental_ReturnsTrue()
    {
        var renter = await SeedRenterWithRentalAsync(RentalStatus.Active);
        Assert.True(await _sut.HasActiveRentalsAsync(renter.Id));
    }

    [Fact]
    public async Task HasActiveRentals_OnlyCompleted_ReturnsFalse()
    {
        var renter = await SeedRenterWithRentalAsync(RentalStatus.Completed);
        Assert.False(await _sut.HasActiveRentalsAsync(renter.Id));
    }

    [Fact]
    public async Task Deactivate_DelistsItems_CancelsOwnedRentals_ReleasesDeposit()
    {
        var owner = EntityFactory.CreateUser();
        var renter = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: owner.Id, isListed: true);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id, status: RentalStatus.Active);
        var deposit = EntityFactory.CreateDeposit(rentalId: rental.Id, status: DepositStatus.Authorized);

        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        _context.RentalDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        TrackUserManagerFindById(owner);
        _userManager.Setup(m => m.UpdateSecurityStampAsync(owner)).ReturnsAsync(IdentityResult.Success);

        await _sut.DeactivateAccountAsync(owner.Id, DeactivationSource.User, "leaving");

        _context.ChangeTracker.Clear();
        Assert.True((await _context.Users.FindAsync(owner.Id))!.IsDeactivated);
        Assert.False((await _context.Items.FindAsync(item.Id))!.IsListed);
        Assert.Equal(RentalStatus.Cancelled, (await _context.Rentals.FindAsync(rental.Id))!.Status);
        Assert.Equal(DepositStatus.Released, (await _context.RentalDeposits.FindAsync(deposit.Id))!.Status);
    }

    [Fact]
    public async Task Reactivate_RelistsOnlyNonHiddenItems()
    {
        var user = EntityFactory.CreateUser(isDeactivated: true);
        var normalItem = EntityFactory.CreateItem(userId: user.Id, isListed: false);
        var hiddenItem = EntityFactory.CreateItem(userId: user.Id, isListed: false);
        hiddenItem.IsAdminHidden = true;

        _context.Users.Add(user);
        _context.Items.AddRange(normalItem, hiddenItem);
        await _context.SaveChangesAsync();

        TrackUserManagerFindById(user);

        await _sut.ReactivateAccountAsync(user.Id);

        _context.ChangeTracker.Clear();
        Assert.False((await _context.Users.FindAsync(user.Id))!.IsDeactivated);
        Assert.True((await _context.Items.FindAsync(normalItem.Id))!.IsListed);
        Assert.False((await _context.Items.FindAsync(hiddenItem.Id))!.IsListed);
    }

    [Fact]
    public async Task Delete_WithActiveRentals_Throws()
    {
        var owner = EntityFactory.CreateUser();
        var renter = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: owner.Id);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id, status: RentalStatus.Active);
        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();

        TrackUserManagerFindById(renter);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAccountAsync(renter.Id));
    }

    [Fact]
    public async Task Delete_NoActiveRentals_AnonymisesUserAndRemovesOwnedData()
    {
        var user = EntityFactory.CreateUser(firstName: "Real", lastName: "Name");
        var otherOwner = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: user.Id);
        var image = EntityFactory.CreateItemImage(itemId: item.Id);
        var favoritedItem = EntityFactory.CreateItem(userId: otherOwner.Id);
        // Completed rental on someone else's item → no active rentals, survives anonymisation.
        var rental = EntityFactory.CreateRental(
            itemId: favoritedItem.Id, renterId: user.Id, ownerId: otherOwner.Id,
            status: RentalStatus.Completed);
        var favorite = new AccountItemFavorite { AccountId = user.Id, ItemId = favoritedItem.Id };
        var review = EntityFactory.CreateReview(itemId: favoritedItem.Id, reviewerId: user.Id, isDeleted: false);
        var payment = new Payment { RentalId = rental.Id, UserId = user.Id, Amount = 10m, Status = PaymentStatus.Success };
        var consent = new CookieConsent { UserId = user.Id, UserAgent = "Mozilla/5.0" };

        _context.Users.AddRange(user, otherOwner);
        _context.Items.AddRange(item, favoritedItem);
        _context.ItemImages.Add(image);
        _context.Rentals.Add(rental);
        _context.AccountItemFavorites.Add(favorite);
        _context.Reviews.Add(review);
        _context.Payments.Add(payment);
        _context.CookieConsents.Add(consent);
        await _context.SaveChangesAsync();

        TrackUserManagerFindById(user);
        _userManager.Setup(m => m.HasPasswordAsync(user)).ReturnsAsync(false);
        _userManager.Setup(m => m.SetEmailAsync(user, It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, e) => u.Email = e).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.SetUserNameAsync(user, It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, n) => u.UserName = n).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.SetLockoutEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset?>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

        await _sut.DeleteAccountAsync(user.Id);

        // Owned-data changes are flushed by the mid-method SaveChanges, so
        // re-query them. The user record itself is persisted by the Identity
        // store (mocked here), so assert its anonymisation on the mutated
        // in-memory instance instead.
        Assert.Equal("Deleted User", user.FirstName);
        Assert.Null(user.LastName);
        Assert.EndsWith(AccountLifecycleService.AnonymisedEmailSuffix, user.Email);

        _context.ChangeTracker.Clear();
        Assert.False(await _context.Items.AnyAsync(i => i.UserId == user.Id));
        Assert.Null((await _context.Payments.FindAsync(payment.Id))!.UserId);
        Assert.False(await _context.AccountItemFavorites.AnyAsync(f => f.AccountId == user.Id));

        var deletedReview = await _context.Reviews.FindAsync(review.Id);
        Assert.True(deletedReview!.IsDeleted);
        Assert.Null(deletedReview.Title);

        var anonConsent = await _context.CookieConsents.FindAsync(consent.Id);
        Assert.Null(anonConsent!.UserId);
        Assert.Null(anonConsent.UserAgent);
    }
}
