using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.Dto;
using RentMate.Services.Interfaces;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services.Implementations
{
    /// <summary>
    /// Computes all dashboard attention counts for a user.
    /// Centralises the queries that previously lived inline in _NavBar.cshtml.
    /// Queries run sequentially because EF Core forbids concurrent operations on a single DbContext.
    /// </summary>
    public class AttentionCountService : IAttentionCountService
    {
        private readonly RentMateContext _db;

        public AttentionCountService(RentMateContext db)
        {
            _db = db;
        }

        public async Task<AttentionCounts> GetForUserAsync(string userId, bool isAdmin = false)
        {
            var today = DateTime.UtcNow.Date;

            var overdue = await _db.Rentals.AsNoTracking()
                .CountAsync(r => r.RenterId == userId
                    && r.Status == RentalStatus.Active
                    && r.EndDate.Date < today);

            var accepted = await _db.Rentals.AsNoTracking()
                .CountAsync(r => r.RenterId == userId && r.Status == RentalStatus.Accepted);

            var pendingRequests = await _db.Rentals.AsNoTracking()
                .CountAsync(r => r.OwnerId == userId && r.Status == RentalStatus.Pending);

            var pendingExtensions = await _db.RentalExtensions.AsNoTracking()
                .CountAsync(e => e.Status == ExtensionStatus.Pending && e.Rental!.OwnerId == userId);

            var renterDeposit = await _db.Rentals.AsNoTracking()
                .CountAsync(r => r.RenterId == userId && r.Deposit != null
                    && (r.Deposit.Status == DepositStatus.Charged
                        || r.Deposit.Status == DepositStatus.PartiallyCharged
                        || r.Deposit.Status == DepositStatus.CounterOffered));

            var ownerDisputed = await _db.Rentals.AsNoTracking()
                .CountAsync(r => r.OwnerId == userId && r.Deposit != null
                    && r.Deposit.Status == DepositStatus.Disputed);

            var extensionPayment = await _db.RentalExtensions.AsNoTracking()
                .CountAsync(e => (e.Status == ExtensionStatus.Accepted || e.Status == ExtensionStatus.AutoApproved)
                    && e.Rental!.RenterId == userId);

            var completedArchivable = await _db.Rentals.AsNoTracking()
                .CountAsync(r => (r.RenterId == userId || r.OwnerId == userId)
                    && r.Status == RentalStatus.Completed
                    && r.ArchivedAt == null
                    && (r.Deposit == null
                        || (r.Deposit.Status != DepositStatus.Disputed
                            && r.Deposit.Status != DepositStatus.CounterOffered
                            && r.Deposit.Status != DepositStatus.Escalated)));

            var adminEscalated = isAdmin
                ? await _db.RentalDeposits.AsNoTracking().CountAsync(d => d.Status == DepositStatus.Escalated)
                : 0;

            return new AttentionCounts(
                Overdue:              overdue,
                Accepted:             accepted,
                PendingRequests:      pendingRequests,
                PendingExtensions:    pendingExtensions,
                RenterDepositAction:  renterDeposit,
                OwnerDisputedDeposits: ownerDisputed,
                ExtensionPayments:    extensionPayment,
                CompletedArchivable:  completedArchivable,
                AdminEscalated:       adminEscalated);
        }
    }
}
