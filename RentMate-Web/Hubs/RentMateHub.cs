using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RentMate.Hubs;

/// <summary>
/// SignalR hub for real-time notifications in the RentMate application.
/// Requires authentication for all connections.
/// </summary>
[Authorize]
public class RentMateHub : Hub
{
    #region Constants

    /// <summary>
    /// Client method name for rental request notifications.
    /// </summary>
    public const string RentalRequestedEvent = "RentalRequested";

    #endregion

    #region Hub Methods

    /// <summary>
    /// Sends a rental request notification to a specific owner.
    /// </summary>
    /// <param name="ownerId">The user ID of the item owner.</param>
    /// <param name="rentalData">The rental information to send.</param>
    public async Task NotifyRentalRequest(string ownerId, object rentalData)
    {
        await Clients.User(ownerId).SendAsync(RentalRequestedEvent, rentalData);
    }

    #endregion
}
