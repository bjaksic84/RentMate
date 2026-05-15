using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RentMate.Models.Domain;
using RentMate.Services.Implementations;
using RentMate.Services.Interfaces;
using RentMate.Tests.Helpers;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;
using NotificationType = RentMate.Models.Domain.NotificationType;

namespace RentMate.Tests.Services;

/// <summary>
/// DataRetentionService is a BackgroundService whose purge logic lives in
/// private methods. Each enforces an irreversible cascading delete, so they
/// are exercised directly via reflection against a SQLite context
/// (PurgeOldNotifications uses ExecuteDeleteAsync, unsupported by InMemory).
/// </summary>
public class DataRetentionServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly SqliteConnection _connection;
    private readonly DataRetentionService _sut;

    public DataRetentionServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqlite();
        _sut = new DataRetentionService(
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<ILogger<DataRetentionService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Task Invoke(string method, params object[] args)
    {
        var m = typeof(DataRetentionService).GetMethod(
            method, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)m.Invoke(_sut, args)!;
    }

    [Fact]
    public async Task PurgeExpiredRentals_RemovesOldFinished_KeepsRecentAndActive()
    {
        var owner = EntityFactory.CreateUser();
        var renter = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: owner.Id);
        var old = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Completed, createdAt: DateTime.UtcNow.AddYears(-6));
        var recent = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Completed, createdAt: DateTime.UtcNow);
        var oldButActive = EntityFactory.CreateRental(
            itemId: item.Id, renterId: renter.Id, ownerId: owner.Id,
            status: RentalStatus.Active, createdAt: DateTime.UtcNow.AddYears(-6));
        _context.Users.AddRange(owner, renter);
        _context.Items.Add(item);
        _context.Rentals.AddRange(old, recent, oldButActive);
        await _context.SaveChangesAsync();

        var fileUpload = new Mock<IFileUploadService>();
        fileUpload.Setup(f => f.DeleteFilesAsync(It.IsAny<IEnumerable<string>>())).Returns(Task.CompletedTask);

        await Invoke("PurgeExpiredRentalsAsync", _context, fileUpload.Object, CancellationToken.None);

        _context.ChangeTracker.Clear();
        Assert.Null(await _context.Rentals.FindAsync(old.Id));
        Assert.NotNull(await _context.Rentals.FindAsync(recent.Id));
        Assert.NotNull(await _context.Rentals.FindAsync(oldButActive.Id));
    }

    [Fact]
    public async Task PurgeDeletedReviews_RemovesOldSoftDeleted_KeepsRecentAndLive()
    {
        var owner = EntityFactory.CreateUser();
        var reviewer = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: owner.Id);
        var oldDeleted = EntityFactory.CreateReview(
            itemId: item.Id, reviewerId: reviewer.Id, isDeleted: true,
            createdAt: DateTime.UtcNow.AddDays(-400));
        var recentDeleted = EntityFactory.CreateReview(
            itemId: item.Id, reviewerId: reviewer.Id, isDeleted: true,
            createdAt: DateTime.UtcNow);
        var oldLive = EntityFactory.CreateReview(
            itemId: item.Id, reviewerId: reviewer.Id, isDeleted: false,
            createdAt: DateTime.UtcNow.AddDays(-400));
        _context.Users.AddRange(owner, reviewer);
        _context.Items.Add(item);
        _context.Reviews.AddRange(oldDeleted, recentDeleted, oldLive);
        await _context.SaveChangesAsync();

        await Invoke("PurgeDeletedReviewsAsync", _context, CancellationToken.None);

        _context.ChangeTracker.Clear();
        Assert.Null(await _context.Reviews.FindAsync(oldDeleted.Id));
        Assert.NotNull(await _context.Reviews.FindAsync(recentDeleted.Id));
        Assert.NotNull(await _context.Reviews.FindAsync(oldLive.Id));
    }

    [Fact]
    public async Task PurgeOldNotifications_RemovesOldDismissedAndAnythingOver90Days()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        var oldDismissed = new Notification
        {
            UserId = user.Id, Type = NotificationType.RentalRequested, Title = "a",
            IsDismissed = true, CreatedAt = DateTime.UtcNow.AddDays(-31)
        };
        var veryOld = new Notification
        {
            UserId = user.Id, Type = NotificationType.RentalRequested, Title = "b",
            IsDismissed = false, CreatedAt = DateTime.UtcNow.AddDays(-91)
        };
        var fresh = new Notification
        {
            UserId = user.Id, Type = NotificationType.RentalRequested, Title = "c",
            IsDismissed = false, CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.AddRange(oldDismissed, veryOld, fresh);
        await _context.SaveChangesAsync();

        await Invoke("PurgeOldNotificationsAsync", _context, CancellationToken.None);

        _context.ChangeTracker.Clear();
        Assert.Null(await _context.Notifications.FindAsync(oldDismissed.Id));
        Assert.Null(await _context.Notifications.FindAsync(veryOld.Id));
        Assert.NotNull(await _context.Notifications.FindAsync(fresh.Id));
    }

    [Fact]
    public async Task PurgeAnonymisedUsers_DeletesTombstonesWithoutRentals_Only()
    {
        var cleanAnon = EntityFactory.CreateUser(
            email: AccountLifecycleService.AnonymisedEmailPrefix + "abc123def456" +
                   AccountLifecycleService.AnonymisedEmailSuffix);
        var anonWithRental = EntityFactory.CreateUser(
            email: AccountLifecycleService.AnonymisedEmailPrefix + "zzz999yyy888" +
                   AccountLifecycleService.AnonymisedEmailSuffix);
        var normalUser = EntityFactory.CreateUser(email: "real@user.com");
        var owner = EntityFactory.CreateUser();
        var item = EntityFactory.CreateItem(userId: owner.Id);
        var rental = EntityFactory.CreateRental(
            itemId: item.Id, renterId: anonWithRental.Id, ownerId: owner.Id,
            status: RentalStatus.Completed);

        _context.Users.AddRange(cleanAnon, anonWithRental, normalUser, owner);
        _context.Items.Add(item);
        _context.Rentals.Add(rental);
        await _context.SaveChangesAsync();

        var userManager = MockUserManager.Create();
        userManager.Setup(m => m.DeleteAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        await Invoke("PurgeAnonymisedUsersAsync", _context, userManager.Object, CancellationToken.None);

        userManager.Verify(m => m.DeleteAsync(It.Is<ApplicationUser>(u => u.Id == cleanAnon.Id)), Times.Once);
        userManager.Verify(m => m.DeleteAsync(It.Is<ApplicationUser>(u => u.Id == anonWithRental.Id)), Times.Never);
        userManager.Verify(m => m.DeleteAsync(It.Is<ApplicationUser>(u => u.Id == normalUser.Id)), Times.Never);
    }
}
