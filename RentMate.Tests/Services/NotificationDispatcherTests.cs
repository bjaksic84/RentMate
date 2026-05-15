using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using RentMate.Hubs;
using RentMate.Models.Domain;
using RentMate.Services.Implementations;
using RentMate.Services.Interfaces;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;
using NotificationType = RentMate.Models.Domain.NotificationType;

namespace RentMate.Tests.Services;

/// <summary>
/// NotificationDispatcher is pure orchestration: every event must push a
/// SignalR message to the right user AND persist a notification of the right
/// type/recipient, and auto-dismiss superseded notifications where applicable.
/// </summary>
public class NotificationDispatcherTests
{
    private readonly Mock<IClientProxy> _clientProxy = new();
    private readonly Mock<IHubClients> _hubClients = new();
    private readonly Mock<IHubContext<RentMateHub>> _hub = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly NotificationDispatcher _sut;

    public NotificationDispatcherTests()
    {
        _hubClients.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxy.Object);
        _hub.Setup(h => h.Clients).Returns(_hubClients.Object);
        _clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var localizer = new Mock<IStringLocalizer<NotificationDispatcher>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string k) => new LocalizedString(k, k));

        _sut = new NotificationDispatcher(_hub.Object, _notifications.Object, localizer.Object);
    }

    [Fact]
    public async Task RentalRequested_NotifiesOwner_AndPushesEvent()
    {
        await _sut.RentalRequestedAsync(42, "owner-1", "Drill", "r@x.com", "Renter",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(2));

        _hubClients.Verify(c => c.User("owner-1"), Times.AtLeastOnce);
        _clientProxy.Verify(p => p.SendCoreAsync(
            RentMateHub.RentalRequestedEvent, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.CreateAsync(
            "owner-1", NotificationType.RentalRequested, It.IsAny<string>(),
            It.IsAny<string?>(), 42, "Rental", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RentalStatusChanged_Accepted_NotifiesRenterAndAutoDismissesRequest()
    {
        await _sut.RentalStatusChangedAsync(7, "renter-1", "Drill", RentalStatus.Accepted);

        _notifications.Verify(n => n.CreateAsync(
            "renter-1", NotificationType.RentalAccepted, It.IsAny<string>(),
            It.IsAny<string?>(), 7, "Rental", It.IsAny<string?>()), Times.Once);
        _notifications.Verify(n => n.AutoDismissAsync(
            7, "Rental", NotificationType.RentalRequested), Times.Once);
    }

    [Fact]
    public async Task RentalStatusChanged_Pending_DoesNotAutoDismiss()
    {
        await _sut.RentalStatusChangedAsync(7, "renter-1", "Drill", RentalStatus.Pending);

        _notifications.Verify(n => n.AutoDismissAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<NotificationType?>()), Times.Never);
    }

    [Fact]
    public async Task DepositCharged_NotifiesRenter()
    {
        await _sut.DepositChargedAsync(99, "renter-9", "Camera", 50m, "Damage");

        _clientProxy.Verify(p => p.SendCoreAsync(
            RentMateHub.DepositStatusChangedEvent, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.CreateAsync(
            "renter-9", NotificationType.DepositCharged, It.IsAny<string>(),
            It.IsAny<string?>(), 99, "Deposit", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task DepositDisputed_NotifiesOwner_AndAutoDismissesChargeNotice()
    {
        await _sut.DepositDisputedAsync(15, "owner-2", "Tent");

        _notifications.Verify(n => n.CreateAsync(
            "owner-2", NotificationType.DepositDisputed, It.IsAny<string>(),
            It.IsAny<string?>(), 15, "Deposit", It.IsAny<string?>()), Times.Once);
        _notifications.Verify(n => n.AutoDismissAsync(
            15, "Deposit", NotificationType.DepositCharged), Times.Once);
    }

    [Fact]
    public async Task ExtensionRequested_NotifiesOwnerWithExtensionReference()
    {
        await _sut.ExtensionRequestedAsync(3, 7, "owner-3", "Ladder",
            DateTime.UtcNow.AddDays(3), autoApproved: false);

        _clientProxy.Verify(p => p.SendCoreAsync(
            RentMateHub.ExtensionRequestedEvent, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.CreateAsync(
            "owner-3", NotificationType.ExtensionRequested, It.IsAny<string>(),
            It.IsAny<string?>(), 3, "Extension", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task DepositAdminResolved_NotifiesBothParties()
    {
        await _sut.DepositAdminResolvedAsync(20, "owner-x", "renter-y", "Bike", 0m, "ok");

        _notifications.Verify(n => n.CreateAsync(
            "owner-x", NotificationType.DepositResolved, It.IsAny<string>(),
            It.IsAny<string?>(), 20, "Deposit", It.IsAny<string?>()), Times.Once);
        _notifications.Verify(n => n.CreateAsync(
            "renter-y", NotificationType.DepositResolved, It.IsAny<string>(),
            It.IsAny<string?>(), 20, "Deposit", It.IsAny<string?>()), Times.Once);
        _notifications.Verify(n => n.AutoDismissAsync(20, "Deposit", null), Times.Once);
    }
}
