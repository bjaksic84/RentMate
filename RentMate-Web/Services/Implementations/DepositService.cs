using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services.Implementations
{
    /// <summary>
    /// Manages the rental deposit lifecycle: authorization, release, and charging.
    /// Delegates actual payment operations to IPaymentService.
    /// </summary>
    public class DepositService : IDepositService
    {
        private readonly RentMateContext _context;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<DepositService> _logger;

        public DepositService(
            RentMateContext context,
            IPaymentService paymentService,
            ILogger<DepositService> logger)
        {
            _context = context;
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> CreateAndAuthorizeDepositAsync(int rentalId, decimal amount)
        {
            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId)
                ?? throw new InvalidOperationException($"Rental {rentalId} not found.");

            var existingDeposit = await _context.RentalDeposits
                .FirstOrDefaultAsync(d => d.RentalId == rentalId);

            if (existingDeposit != null)
                throw new InvalidOperationException($"Deposit already exists for rental {rentalId}.");

            var deposit = new RentalDeposit
            {
                RentalId = rentalId,
                Amount = amount,
                Status = DepositStatus.Pending
            };

            _context.RentalDeposits.Add(deposit);
            await _context.SaveChangesAsync();

            // Authorize via payment provider
            var paymentResult = await _paymentService.AuthorizeAsync(
                rental.RenterId,
                amount,
                $"Deposit for rental #{rentalId} - {rental.Item?.Title}");

            if (paymentResult.Success)
            {
                deposit.Status = DepositStatus.Authorized;
                deposit.PaymentReference = paymentResult.PaymentReference;
                deposit.AuthorizedAt = DateTime.UtcNow;
                deposit.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _logger.LogWarning(
                    "Failed to authorize deposit for rental {RentalId}: {Error}",
                    rentalId, paymentResult.ErrorMessage);
                throw new InvalidOperationException(
                    $"Deposit authorization failed: {paymentResult.ErrorMessage}");
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit of {Amount:C} authorized for rental {RentalId}",
                amount, rentalId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> ReleaseDepositAsync(int rentalId, string releasedByUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental).ThenInclude(r => r!.Item)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.Authorized)
                throw new InvalidOperationException(
                    $"Cannot release deposit in status {deposit.Status}.");

            // Verify the user is the owner
            if (deposit.Rental?.OwnerId != releasedByUserId)
                throw new UnauthorizedAccessException("Only the item owner can release a deposit.");

            if (deposit.PaymentReference != null)
            {
                var result = await _paymentService.ReleaseAsync(deposit.PaymentReference);
                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Failed to release deposit payment for rental {RentalId}: {Error}",
                        rentalId, result.ErrorMessage);
                    throw new InvalidOperationException(
                        $"Deposit release failed: {result.ErrorMessage}");
                }
            }

            deposit.Status = DepositStatus.Released;
            deposit.ReleasedAt = DateTime.UtcNow;
            deposit.UpdatedAt = DateTime.UtcNow;

            // Mark rental as completed when owner releases deposit (premature end)
            var rental = deposit.Rental!;
            rental.Status = RentalStatus.Completed;
            rental.EndDate = DateTime.UtcNow;
            rental.UpdatedAt = DateTime.UtcNow;
            if (rental.Item != null)
                rental.Item.IsRented = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit of {Amount:C} released for rental {RentalId} by user {UserId}; rental marked completed.",
                deposit.Amount, rentalId, releasedByUserId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> ChargeDepositAsync(
            int rentalId, decimal amount, string reason, string chargedByUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental).ThenInclude(r => r!.Item)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.Authorized)
                throw new InvalidOperationException(
                    $"Cannot charge deposit in status {deposit.Status}.");

            if (amount > deposit.Amount)
                throw new InvalidOperationException(
                    $"Charge amount ({amount:C}) exceeds deposit ({deposit.Amount:C}).");

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason must be provided for charging a deposit.");

            // Verify the user is the owner
            if (deposit.Rental?.OwnerId != chargedByUserId)
                throw new UnauthorizedAccessException("Only the item owner can charge a deposit.");

            if (deposit.PaymentReference != null)
            {
                var captureResult = await _paymentService.CaptureAsync(
                    deposit.PaymentReference, amount);
                if (!captureResult.Success)
                {
                    _logger.LogWarning(
                        "Failed to capture deposit for rental {RentalId}: {Error}",
                        rentalId, captureResult.ErrorMessage);
                    throw new InvalidOperationException(
                        $"Deposit capture failed: {captureResult.ErrorMessage}");
                }

                // Release remaining amount if partial charge
                if (amount < deposit.Amount)
                {
                    await _paymentService.ReleaseAsync(deposit.PaymentReference);
                }
            }

            deposit.ChargedAmount = amount;
            deposit.ChargeReason = reason;
            deposit.ChargedAt = DateTime.UtcNow;
            deposit.Status = amount < deposit.Amount
                ? DepositStatus.PartiallyCharged
                : DepositStatus.Charged;
            deposit.UpdatedAt = DateTime.UtcNow;

            // Mark rental as completed when owner charges deposit (premature end)
            var rental = deposit.Rental!;
            rental.Status = RentalStatus.Completed;
            rental.EndDate = DateTime.UtcNow;
            rental.UpdatedAt = DateTime.UtcNow;
            if (rental.Item != null)
                rental.Item.IsRented = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit charged {ChargedAmount:C} of {TotalAmount:C} for rental {RentalId}. Reason: {Reason}; rental marked completed.",
                amount, deposit.Amount, rentalId, reason);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit?> GetDepositForRentalAsync(int rentalId)
        {
            return await _context.RentalDeposits
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.RentalId == rentalId);
        }

        /// <inheritdoc/>
        public async Task<DepositSummary> GetDepositSummaryForOwnerAsync(string ownerUserId)
        {
            var deposits = await _context.RentalDeposits
                .AsNoTracking()
                .Include(d => d.Rental)
                .Where(d => d.Rental!.OwnerId == ownerUserId)
                .ToListAsync();

            return new DepositSummary
            {
                TotalHeld = deposits
                    .Where(d => d.Status == DepositStatus.Authorized)
                    .Sum(d => d.Amount),
                ActiveDepositCount = deposits
                    .Count(d => d.Status == DepositStatus.Authorized),
                TotalCharged = deposits
                    .Where(d => d.Status == DepositStatus.Charged || d.Status == DepositStatus.PartiallyCharged)
                    .Sum(d => d.ChargedAmount ?? 0),
                TotalReleased = deposits
                    .Where(d => d.Status == DepositStatus.Released)
                    .Sum(d => d.Amount)
            };
        }
    }
}
