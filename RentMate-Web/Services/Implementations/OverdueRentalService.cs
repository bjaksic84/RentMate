using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RentMate.Hubs;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

namespace RentMate.Services.Implementations
{
    /// <summary>
    /// Background service that periodically checks for overdue rentals
    /// and sends SignalR notifications to both owners and renters.
    /// </summary>
    public class OverdueRentalService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OverdueRentalService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        public OverdueRentalService(
            IServiceScopeFactory scopeFactory,
            ILogger<OverdueRentalService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Overdue rental detection service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckOverdueRentalsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking overdue rentals.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        private async Task CheckOverdueRentalsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RentMateContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<RentMateHub>>();

            var now = DateTime.UtcNow.Date;

            var overdueRentals = await context.Rentals
                .Include(r => r.Item)
                .Where(r => r.Status == RentalStatus.Active && r.EndDate.Date < now)
                .ToListAsync(ct);

            if (overdueRentals.Count == 0) return;

            _logger.LogInformation("Found {Count} overdue rentals.", overdueRentals.Count);

            foreach (var rental in overdueRentals)
            {
                var daysOverdue = (now - rental.EndDate.Date).Days;
                var data = new
                {
                    rentalId = rental.Id,
                    itemTitle = rental.Item?.Title,
                    daysOverdue,
                    endDate = rental.EndDate.ToString("yyyy-MM-dd")
                };

                // Notify the renter
                if (!string.IsNullOrEmpty(rental.RenterId))
                {
                    await hubContext.Clients.User(rental.RenterId).SendAsync(
                        RentMateHub.RentalOverdueEvent, data, ct);
                }

                // Notify the owner
                if (!string.IsNullOrEmpty(rental.OwnerId))
                {
                    await hubContext.Clients.User(rental.OwnerId).SendAsync(
                        RentMateHub.RentalOverdueEvent, data, ct);
                }
            }
        }
    }
}
