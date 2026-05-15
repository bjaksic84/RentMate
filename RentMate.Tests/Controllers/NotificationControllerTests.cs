using System.Net;
using RentMate.Models.Domain;
using RentMate.Tests.Helpers;
using RentMate.Tests.Infrastructure;
using NotificationType = RentMate.Models.Domain.NotificationType;

namespace RentMate.Tests.Controllers;

public class NotificationControllerTests : IntegrationTestBase
{
    private readonly string _userId;
    private readonly int _unreadId;

    public NotificationControllerTests(RentMateWebApplicationFactory factory) : base(factory)
    {
        var user = EntityFactory.CreateUser(firstName: "Notif", onboardingCompleted: true);
        _userId = user.Id;

        var unread = new Notification
        {
            UserId = user.Id, Type = NotificationType.RentalRequested,
            Title = "Unread one", IsRead = false, IsDismissed = false
        };
        var read = new Notification
        {
            UserId = user.Id, Type = NotificationType.RentalRequested,
            Title = "Already read", IsRead = true, IsDismissed = false
        };

        SeedData(ctx =>
        {
            ctx.Users.Add(user);
            ctx.Notifications.AddRange(unread, read);
        });

        // Identity Id is assigned by SaveChanges inside SeedData.
        _unreadId = unread.Id;
    }

    [Fact]
    public async Task UnreadCount_ReturnsOnlyUnreadNonDismissed()
    {
        AuthenticateAs(_userId);
        var response = await Client.GetAsync("/Notification/UnreadCount");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"count\":1", content.Replace(" ", ""));
    }

    [Fact]
    public async Task UnreadCount_Unauthenticated_IsRejected()
    {
        ClearAuthentication();
        var response = await Client.GetAsync("/Notification/UnreadCount");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect,
            $"Expected 401/302, got {response.StatusCode}");
    }

    [Fact]
    public async Task MarkAsRead_SetsIsReadInDb()
    {
        AuthenticateAs(_userId);
        var response = await PostJsonAsync("/Notification/MarkAsRead", new { id = _unreadId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var db = GetDbContext();
        Assert.True((await db.Notifications.FindAsync(_unreadId))!.IsRead);
    }

    [Fact]
    public async Task Dismiss_SetsIsDismissedInDb()
    {
        AuthenticateAs(_userId);
        var response = await PostJsonAsync("/Notification/Dismiss", new { id = _unreadId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var db = GetDbContext();
        Assert.True((await db.Notifications.FindAsync(_unreadId))!.IsDismissed);
    }

    [Fact]
    public async Task MarkAllAsRead_ClearsUnreadCount()
    {
        AuthenticateAs(_userId);
        var response = await PostJsonAsync("/Notification/MarkAllAsRead", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var db = GetDbContext();
        Assert.False(await db.Notifications.AnyAsync(n => n.UserId == _userId && !n.IsRead));
    }
}
