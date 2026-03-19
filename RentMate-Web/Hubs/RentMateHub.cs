using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using RentMate.Models.Domain;

namespace RentMate.Hubs;

/// <summary>
/// SignalR hub for real-time notifications in the RentMate application.
/// Requires authentication for all connections.
/// </summary>
[Authorize]
public class RentMateHub(UserManager<ApplicationUser> userManager) : Hub
{
    #region Constants

    /// <summary>
    /// Client method name for rental request notifications.
    /// </summary>
    public const string RentalRequestedEvent = "RentalRequested";

    /// <summary>
    /// Client method name for rental status change notifications.
    /// </summary>
    public const string RentalStatusChangedEvent = "RentalStatusChanged";

    /// <summary>
    /// Client method name for extension request notifications sent to the owner.
    /// </summary>
    public const string ExtensionRequestedEvent = "ExtensionRequested";

    /// <summary>
    /// Client method name for extension status change notifications sent to the renter.
    /// </summary>
    public const string ExtensionStatusChangedEvent = "ExtensionStatusChanged";

    /// <summary>
    /// Client method name for deposit status change notifications.
    /// </summary>
    public const string DepositStatusChangedEvent = "DepositStatusChanged";

    /// <summary>
    /// Client method name for overdue rental notifications sent to both parties.
    /// </summary>
    public const string RentalOverdueEvent = "RentalOverdue";

    #endregion

    // No public hub methods — all notifications are dispatched server-side
    // via IHubContext<RentMateHub>.Clients.User(id).SendAsync().
    // Keeping the hub empty prevents authenticated clients from invoking
    // notification methods with arbitrary target user IDs and payloads.

    /// <summary>
    /// Reject connections from deactivated accounts on connect and reconnect.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is { IsDeactivated: true })
            {
                Context.Abort();
                return;
            }
        }

        await base.OnConnectedAsync();
    }
}

