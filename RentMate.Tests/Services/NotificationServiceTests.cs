using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using RentMate.Hubs;
using RentMate.Tests.Helpers;
using NotificationType = RentMate.Models.Domain.NotificationType;

namespace RentMate.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly RentMateContext _context;
    private readonly SqliteConnection? _connection;
    private readonly Mock<IHubContext<RentMateHub>> _hubMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        // Use SQLite because MarkAllAsReadAsync uses ExecuteUpdateAsync
        (_context, _connection) = TestDbContextFactory.CreateSqlite();

        _hubMock = new Mock<IHubContext<RentMateHub>>();
        var clientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxyMock.Object);

        _sut = new NotificationService(_context, _hubMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection?.Dispose();
    }

    /// <summary>Seeds a notification directly into the database.</summary>
    private async Task<Notification> SeedNotificationAsync(
        string userId,
        NotificationType type = NotificationType.RentalRequested,
        string title = "Test notification",
        string? message = null,
        int? referenceId = null,
        string? referenceType = null,
        string? actionUrl = null,
        bool isRead = false,
        bool isDismissed = false,
        DateTime? createdAt = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            ActionUrl = actionUrl,
            IsRead = isRead,
            IsDismissed = isDismissed,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        return notification;
    }

    // ================================================================
    //  CreateAsync
    // ================================================================

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_SavesNotification_ToDatabase()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _sut.CreateAsync(user.Id, NotificationType.RentalRequested,
            "New rental request", "Someone wants your item", 42, "Rental", "/rentals/42");

        var saved = await _context.Notifications.FirstOrDefaultAsync(n => n.UserId == user.Id);
        Assert.NotNull(saved);
        Assert.Equal(NotificationType.RentalRequested, saved.Type);
        Assert.Equal("New rental request", saved.Title);
        Assert.Equal("Someone wants your item", saved.Message);
        Assert.Equal(42, saved.ReferenceId);
        Assert.Equal("Rental", saved.ReferenceType);
        Assert.Equal("/rentals/42", saved.ActionUrl);
        Assert.False(saved.IsRead);
        Assert.False(saved.IsDismissed);
    }

    [Fact]
    public async Task CreateAsync_PushesSignalR_NewNotificationEvent()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _sut.CreateAsync(user.Id, NotificationType.ReviewReceived, "New review");

        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                RentMateHub.NewNotificationEvent,
                It.Is<object?[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    #endregion

    // ================================================================
    //  GetUnreadCountAsync
    // ================================================================

    #region GetUnreadCountAsync

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await SeedNotificationAsync(user.Id, isRead: false);
        await SeedNotificationAsync(user.Id, isRead: false);
        await SeedNotificationAsync(user.Id, isRead: true);

        var count = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ExcludesDismissed()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await SeedNotificationAsync(user.Id, isRead: false);
        await SeedNotificationAsync(user.Id, isRead: false, isDismissed: true);

        var count = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(1, count);
    }

    #endregion

    // ================================================================
    //  GetRecentAsync
    // ================================================================

    #region GetRecentAsync

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var older = await SeedNotificationAsync(user.Id, title: "Older",
            createdAt: DateTime.UtcNow.AddHours(-2));
        var newer = await SeedNotificationAsync(user.Id, title: "Newer",
            createdAt: DateTime.UtcNow.AddHours(-1));

        var results = await _sut.GetRecentAsync(user.Id);

        Assert.Equal(2, results.Count);
        Assert.Equal("Newer", results[0].Title);
        Assert.Equal("Older", results[1].Title);
    }

    [Fact]
    public async Task GetRecentAsync_ExcludesDismissed()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await SeedNotificationAsync(user.Id, title: "Visible");
        await SeedNotificationAsync(user.Id, title: "Dismissed", isDismissed: true);

        var results = await _sut.GetRecentAsync(user.Id);

        Assert.Single(results);
        Assert.Equal("Visible", results[0].Title);
    }

    #endregion

    // ================================================================
    //  MarkAsReadAsync
    // ================================================================

    #region MarkAsReadAsync

    [Fact]
    public async Task MarkAsReadAsync_SetsIsReadAndReadAt()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var notification = await SeedNotificationAsync(user.Id);

        await _sut.MarkAsReadAsync(notification.Id, user.Id);

        var updated = await _context.Notifications.FindAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkAsReadAsync_WrongUser_DoesNothing()
    {
        var user = EntityFactory.CreateUser();
        var otherUser = EntityFactory.CreateUser();
        _context.Users.AddRange(user, otherUser);
        await _context.SaveChangesAsync();

        var notification = await SeedNotificationAsync(user.Id);

        await _sut.MarkAsReadAsync(notification.Id, otherUser.Id);

        var unchanged = await _context.Notifications.FindAsync(notification.Id);
        Assert.NotNull(unchanged);
        Assert.False(unchanged.IsRead);
        Assert.Null(unchanged.ReadAt);
    }

    #endregion

    // ================================================================
    //  DismissAsync
    // ================================================================

    #region DismissAsync

    [Fact]
    public async Task DismissAsync_SetsIsDismissed()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var notification = await SeedNotificationAsync(user.Id);

        await _sut.DismissAsync(notification.Id, user.Id);

        var updated = await _context.Notifications.FindAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsDismissed);
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    #endregion

    // ================================================================
    //  MarkAllAsReadAsync
    // ================================================================

    #region MarkAllAsReadAsync

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllUnread()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await SeedNotificationAsync(user.Id, isRead: false);
        await SeedNotificationAsync(user.Id, isRead: false);
        await SeedNotificationAsync(user.Id, isRead: true); // already read

        await _sut.MarkAllAsReadAsync(user.Id);

        var unreadCount = await _context.Notifications
            .CountAsync(n => n.UserId == user.Id && !n.IsRead);
        Assert.Equal(0, unreadCount);
    }

    #endregion

    // ================================================================
    //  AutoDismissAsync
    // ================================================================

    #region AutoDismissAsync

    [Fact]
    public async Task AutoDismissAsync_DismissesMatchingNotifications()
    {
        var user = EntityFactory.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var matching = await SeedNotificationAsync(user.Id,
            type: NotificationType.RentalRequested,
            referenceId: 99, referenceType: "Rental");
        var nonMatching = await SeedNotificationAsync(user.Id,
            type: NotificationType.ReviewReceived,
            referenceId: 100, referenceType: "Review");

        await _sut.AutoDismissAsync(99, "Rental");

        var dismissed = await _context.Notifications.FindAsync(matching.Id);
        var untouched = await _context.Notifications.FindAsync(nonMatching.Id);

        Assert.NotNull(dismissed);
        Assert.True(dismissed.IsDismissed);
        Assert.True(dismissed.IsRead);

        Assert.NotNull(untouched);
        Assert.False(untouched.IsDismissed);
    }

    #endregion
}
