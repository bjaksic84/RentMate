using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RentMate.Hubs
{
    [Authorize]
    public class RentMateHub : Hub
    {
        // You can use this later to send specific notifications manually if needed.
        public async Task NotifyRentalRequest(string ownerId, object rentalData)
        {
            await Clients.User(ownerId).SendAsync("RentalRequested", rentalData);
        }
    }
}
