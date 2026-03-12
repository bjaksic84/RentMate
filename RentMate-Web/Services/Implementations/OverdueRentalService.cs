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
                    await CheckDisputeDeadlinesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking overdue rentals or dispute deadlines.");
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

        private async Task CheckDisputeDeadlinesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RentMateContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<RentMateHub>>();

            var now = DateTime.UtcNow;

            var expiredDeposits = await context.RentalDeposits
                .Include(d => d.Rental).ThenInclude(r => r!.Item)
                .Where(d => d.DisputeDeadline != null && d.DisputeDeadline < now)
                .ToListAsync(ct);

            if (expiredDeposits.Count == 0) return;

            _logger.LogInformation("Found {Count} deposits with expired deadlines.", expiredDeposits.Count);

            foreach (var deposit in expiredDeposits)
            {
                var rental = deposit.Rental;
                string? notifyUserId = null;
                string statusText = "";

                switch (deposit.Status)
                {
                    case DepositStatus.Charged:
                    case DepositStatus.PartiallyCharged:
                        // Renter didn't dispute in time → charge accepted (final)
                        deposit.DisputeDeadline = null;
                        deposit.UpdatedAt = now;
                        notifyUserId = rental?.OwnerId;
                        statusText = "ChargeAccepted";
                        _logger.LogWarning(
                            "Auto-accepted charge for rental {RentalId}: renter did not dispute within deadline. Amount: {Amount}",
                            deposit.RentalId, deposit.ChargedAmount);
                        break;

                    case DepositStatus.Disputed:
                        // Owner didn't respond in time → release deposit
                        deposit.Status = DepositStatus.Released;
                        deposit.ReleasedAt = now;
                        deposit.ChargedAmount = null;
                        deposit.DisputeDeadline = null;
                        deposit.UpdatedAt = now;
                        notifyUserId = rental?.RenterId;
                        statusText = "Released";
                        _logger.LogWarning(
                            "Auto-released deposit for rental {RentalId}: owner missed dispute response deadline. Original charge: {Amount}",
                            deposit.RentalId, deposit.ChargedAmount);
                        break;

                    case DepositStatus.CounterOffered:
                        // Renter didn't respond to counter-offer → accept counter amount
                        deposit.ChargedAmount = deposit.CounterOfferAmount;
                        deposit.Status = deposit.CounterOfferAmount < deposit.Amount
                            ? DepositStatus.PartiallyCharged
                            : DepositStatus.Charged;
                        deposit.DisputeDeadline = null;
                        deposit.UpdatedAt = now;
                        notifyUserId = rental?.OwnerId;
                        statusText = "CounterAccepted";
                        _logger.LogWarning(
                            "Auto-accepted counter-offer for rental {RentalId}: renter missed response deadline. Counter amount: {Amount}",
                            deposit.RentalId, deposit.CounterOfferAmount);
                        break;

                    default:
                        // Escalated or other states with deadline shouldn't happen, but clear it
                        deposit.DisputeDeadline = null;
                        deposit.UpdatedAt = now;
                        break;
                }

                // Send SignalR notification
                if (!string.IsNullOrEmpty(notifyUserId))
                {
                    await hubContext.Clients.User(notifyUserId).SendAsync(
                        RentMateHub.DepositStatusChangedEvent,
                        new
                        {
                            rentalId = deposit.RentalId,
                            status = statusText,
                            itemTitle = rental?.Item?.Title
                        }, ct);
                }
            }

            await context.SaveChangesAsync(ct);
        }
    }
}
