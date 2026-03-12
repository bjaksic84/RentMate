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
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<DepositService> _logger;

        public DepositService(
            RentMateContext context,
            IPaymentService paymentService,
            IFileUploadService fileUploadService,
            ILogger<DepositService> logger)
        {
            _context = context;
            _paymentService = paymentService;
            _fileUploadService = fileUploadService;
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
                "Deposit of \u20ac{Amount:N2} authorized for rental {RentalId}",
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
                        "Stripe release skipped for rental {RentalId}: {Error}. Proceeding with application-level release.",
                        rentalId, result.ErrorMessage);
                }
            }

            deposit.Status = DepositStatus.Released;
            deposit.ReleasedAt = DateTime.UtcNow;
            deposit.UpdatedAt = DateTime.UtcNow;

            // Mark rental as completed when owner releases deposit (premature end)
            var rental = deposit.Rental!;
            rental.Status = RentalStatus.Completed;
            if (DateTime.UtcNow < rental.EndDate)
                rental.EndDate = DateTime.UtcNow;
            rental.UpdatedAt = DateTime.UtcNow;
            if (rental.Item != null)
                rental.Item.IsRented = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit of \u20ac{Amount:N2} released for rental {RentalId} by user {UserId}; rental marked completed.",
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
                    $"Charge amount (\u20ac{amount:N2}) exceeds deposit (\u20ac{deposit.Amount:N2}).");

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
                    // If the PaymentIntent was never confirmed by the customer (no payment method attached),
                    // log the issue but allow the deposit charge to proceed at the application level.
                    // This happens when the deposit authorization hold was not completed via checkout.
                    _logger.LogWarning(
                        "Stripe capture skipped for rental {RentalId}: {Error}. Proceeding with application-level charge.",
                        rentalId, captureResult.ErrorMessage);
                }
                else
                {
                    // Release remaining amount if partial charge
                    if (amount < deposit.Amount)
                    {
                        await _paymentService.ReleaseAsync(deposit.PaymentReference);
                    }
                }
            }

            deposit.ChargedAmount = amount;
            deposit.ChargeReason = reason;
            deposit.ChargedAt = DateTime.UtcNow;
            deposit.Status = amount < deposit.Amount
                ? DepositStatus.PartiallyCharged
                : DepositStatus.Charged;
            deposit.DisputeDeadline = DateTime.UtcNow.AddDays(5);
            deposit.UpdatedAt = DateTime.UtcNow;

            // Mark rental as completed when owner charges deposit (premature end)
            var rental = deposit.Rental!;
            rental.Status = RentalStatus.Completed;
            if (DateTime.UtcNow < rental.EndDate)
                rental.EndDate = DateTime.UtcNow;
            rental.UpdatedAt = DateTime.UtcNow;
            if (rental.Item != null)
                rental.Item.IsRented = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit charged \u20ac{ChargedAmount:N2} of \u20ac{TotalAmount:N2} for rental {RentalId}. Reason: {Reason}; rental marked completed.",
                amount, deposit.Amount, rentalId, reason);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> DisputeDepositAsync(int rentalId, string reason, string disputedByUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.Charged && deposit.Status != DepositStatus.PartiallyCharged)
                throw new InvalidOperationException(
                    $"Cannot dispute deposit in status {deposit.Status}.");

            if (!deposit.DisputeDeadline.HasValue)
                throw new InvalidOperationException("No dispute window is open for this deposit.");

            if (deposit.DisputeDeadline.Value < DateTime.UtcNow)
                throw new InvalidOperationException("The dispute window has expired.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason must be provided for disputing a deposit.");

            if (deposit.Rental?.RenterId != disputedByUserId)
                throw new UnauthorizedAccessException("Only the renter can dispute a deposit charge.");

            deposit.Status = DepositStatus.Disputed;
            deposit.DisputeReason = reason;
            deposit.DisputedAt = DateTime.UtcNow;
            deposit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit for rental {RentalId} disputed by user {UserId}. Reason: {Reason}",
                rentalId, disputedByUserId, reason);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> ReleaseDisputedDepositAsync(int rentalId, string ownerUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental).ThenInclude(r => r!.Item)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.Disputed)
                throw new InvalidOperationException(
                    $"Cannot release deposit in status {deposit.Status}. Must be Disputed.");

            if (deposit.Rental?.OwnerId != ownerUserId)
                throw new UnauthorizedAccessException("Only the item owner can release a disputed deposit.");

            if (deposit.PaymentReference != null)
            {
                var result = await _paymentService.ReleaseAsync(deposit.PaymentReference);
                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Stripe release skipped for disputed deposit on rental {RentalId}: {Error}. Proceeding with application-level release.",
                        rentalId, result.ErrorMessage);
                }
            }

            deposit.Status = DepositStatus.Released;
            deposit.ReleasedAt = DateTime.UtcNow;
            deposit.ChargedAmount = null;
            deposit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Disputed deposit for rental {RentalId} released by owner {UserId}",
                rentalId, ownerUserId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> AcceptChargeAsync(int rentalId, string renterUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.Charged && deposit.Status != DepositStatus.PartiallyCharged)
                throw new InvalidOperationException(
                    $"Cannot accept charge in status {deposit.Status}.");

            if (deposit.Rental?.RenterId != renterUserId)
                throw new UnauthorizedAccessException("Only the renter can accept a deposit charge.");

            deposit.DisputeDeadline = null;
            deposit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit charge accepted by renter for rental {RentalId}",
                rentalId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> CounterOfferDepositAsync(
            int rentalId, decimal amount, string response, string ownerUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.Disputed)
                throw new InvalidOperationException(
                    $"Cannot counter-offer deposit in status {deposit.Status}.");

            if (deposit.Rental?.OwnerId != ownerUserId)
                throw new UnauthorizedAccessException("Only the item owner can make a counter-offer.");

            if (amount <= 0 || deposit.ChargedAmount == null || amount >= deposit.ChargedAmount.Value)
                throw new InvalidOperationException(
                    "Counter-offer amount must be between 0.01 and less than the charged amount.");

            deposit.Status = DepositStatus.CounterOffered;
            deposit.CounterOfferAmount = amount;
            deposit.OwnerDisputeResponse = response;
            deposit.CounterOfferAt = DateTime.UtcNow;
            deposit.DisputeDeadline = DateTime.UtcNow.AddDays(3);
            deposit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Owner counter-offered \u20ac{Amount:N2} for rental {RentalId} deposit",
                amount, rentalId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> EscalateDisputeAsync(int rentalId, string userId, string? response = null)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            // Allow owner to escalate from Disputed
            // Allow renter to escalate from CounterOffered
            bool isOwner = deposit.Rental?.OwnerId == userId;
            bool isRenter = deposit.Rental?.RenterId == userId;

            if (!isOwner && !isRenter)
                throw new UnauthorizedAccessException("Not authorized to escalate this dispute.");



            if (isOwner && deposit.Status != DepositStatus.Disputed)
                throw new InvalidOperationException($"Owner cannot escalate deposit in status {deposit.Status}. Must be Disputed.");
            
            if (isRenter && deposit.Status != DepositStatus.CounterOffered && deposit.Status != DepositStatus.Disputed)
                throw new InvalidOperationException($"Renter cannot escalate deposit in status {deposit.Status}. Must be Disputed or CounterOffered.");

            deposit.Status = DepositStatus.Escalated;
            deposit.EscalatedAt = DateTime.UtcNow;
            deposit.DisputeDeadline = null;
            deposit.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(response))
            {
                if (isOwner)
                {
                    deposit.OwnerDisputeResponse = response;
                }
                else if (isRenter)
                {
                    deposit.DisputeReason = string.IsNullOrEmpty(deposit.DisputeReason)
                        ? response
                        : deposit.DisputeReason + " | Escalation: " + response;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit dispute escalated to admin for rental {RentalId} by user {UserId}",
                rentalId, userId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> AcceptCounterOfferAsync(int rentalId, string renterUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.CounterOffered)
                throw new InvalidOperationException(
                    $"Cannot accept counter-offer in status {deposit.Status}.");

            if (deposit.Rental?.RenterId != renterUserId)
                throw new UnauthorizedAccessException("Only the renter can accept a counter-offer.");

            deposit.ChargedAmount = deposit.CounterOfferAmount;
            deposit.Status = deposit.CounterOfferAmount < deposit.Amount
                ? DepositStatus.PartiallyCharged
                : DepositStatus.Charged;
            deposit.DisputeDeadline = null;
            deposit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Renter accepted counter-offer of \u20ac{Amount:N2} for rental {RentalId}",
                deposit.CounterOfferAmount, rentalId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> RejectCounterOfferAsync(int rentalId, string renterUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.CounterOffered)
                throw new InvalidOperationException(
                    $"Cannot reject counter-offer in status {deposit.Status}.");

            if (deposit.Rental?.RenterId != renterUserId)
                throw new UnauthorizedAccessException("Only the renter can reject a counter-offer.");

            deposit.Status = (deposit.ChargedAmount ?? deposit.Amount) < deposit.Amount
                ? DepositStatus.PartiallyCharged
                : DepositStatus.Charged;
            deposit.DisputeDeadline = DateTime.UtcNow.AddDays(5);
            deposit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Renter rejected counter-offer for rental {RentalId}. Original charge stands with new 5-day dispute window.",
                rentalId);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<RentalDeposit> AdminResolveDisputeAsync(
            int rentalId, decimal amount, string adminNotes, string adminUserId)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental).ThenInclude(r => r!.Item)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            if (deposit.Status != DepositStatus.Escalated)
                throw new InvalidOperationException(
                    $"Cannot admin-resolve deposit in status {deposit.Status}.");

            // Validate amount before any state changes
            if (amount < 0)
                throw new InvalidOperationException("Resolved amount cannot be negative.");
            if (amount > deposit.Amount)
                throw new InvalidOperationException($"Resolved amount (\u20ac{amount:N2}) cannot exceed original deposit (\u20ac{deposit.Amount:N2}).");

            deposit.AdminNotes = adminNotes;
            deposit.AdminResolvedByUserId = adminUserId;
            deposit.AdminResolvedAt = DateTime.UtcNow;
            deposit.UpdatedAt = DateTime.UtcNow;

            if (amount == 0)
            {
                deposit.Status = DepositStatus.Released;
                deposit.ReleasedAt = DateTime.UtcNow;
                deposit.ChargedAmount = null;

                if (deposit.PaymentReference != null)
                {
                    var result = await _paymentService.ReleaseAsync(deposit.PaymentReference);
                    if (!result.Success)
                    {
                        _logger.LogWarning(
                            "Stripe release skipped for admin-resolved deposit on rental {RentalId}: {Error}",
                            rentalId, result.ErrorMessage);
                    }
                }
            }
            else
            {
                // Partial or Full Uphold
                deposit.Status = DepositStatus.ChargeUpheld;
                deposit.ChargedAmount = amount;
                deposit.ChargedAt = DateTime.UtcNow;

                if (deposit.PaymentReference != null)
                {
                    var captureResult = await _paymentService.CaptureAsync(deposit.PaymentReference, amount);
                    if (!captureResult.Success)
                    {
                        _logger.LogWarning(
                            "Stripe capture skipped for admin-resolved deposit on rental {RentalId}: {Error}. Proceeding with application-level resolution.",
                            rentalId, captureResult.ErrorMessage);
                    }
                    else if (amount < deposit.Amount)
                    {
                        // Release remainder if partial capture
                        await _paymentService.ReleaseAsync(deposit.PaymentReference);
                    }
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Admin resolved deposit for rental {RentalId}: Amount \u20ac{Amount:N2}. Notes: {Notes}",
                rentalId, amount, adminNotes);

            return deposit;
        }

        /// <inheritdoc/>
        public async Task<List<RentalDeposit>> GetEscalatedDisputesAsync()
        {
            return await _context.RentalDeposits
                .AsNoTracking()
                .Include(d => d.Rental).ThenInclude(r => r!.Item)
                .Include(d => d.Rental).ThenInclude(r => r!.Renter)
                .Include(d => d.Rental).ThenInclude(r => r!.Owner)
                .Include(d => d.Evidence)
                .Where(d => d.Status == DepositStatus.Escalated)
                .OrderByDescending(d => d.EscalatedAt)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<List<RentalDeposit>> GetResolvedDisputesAsync()
        {
            return await _context.RentalDeposits
                .AsNoTracking()
                .Include(d => d.Rental).ThenInclude(r => r!.Item)
                .Include(d => d.Rental).ThenInclude(r => r!.Renter)
                .Include(d => d.Rental).ThenInclude(r => r!.Owner)
                .Include(d => d.Evidence)
                .Where(d => d.AdminResolvedAt != null)
                .OrderByDescending(d => d.AdminResolvedAt)
                .ToListAsync();
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

        /// <inheritdoc/>
        public async Task<DisputeEvidence> UploadEvidenceAsync(int rentalId, string userId, Microsoft.AspNetCore.Http.IFormFile file, string? notes)
        {
            var deposit = await _context.RentalDeposits
                .Include(d => d.Rental)
                .FirstOrDefaultAsync(d => d.RentalId == rentalId)
                ?? throw new InvalidOperationException($"No deposit found for rental {rentalId}.");

            // Status check — evidence only makes sense for active charge/dispute flows
            var validStatuses = new[]
            {
                DepositStatus.Charged, DepositStatus.PartiallyCharged,
                DepositStatus.Disputed, DepositStatus.CounterOffered, DepositStatus.Escalated
            };
            if (!validStatuses.Contains(deposit.Status))
                throw new InvalidOperationException(
                    $"Cannot upload evidence for deposit in status {deposit.Status}.");

            // Authorization check
            bool isOwner = deposit.Rental?.OwnerId == userId;
            bool isRenter = deposit.Rental?.RenterId == userId;

            if (!isOwner && !isRenter)
                throw new UnauthorizedAccessException("Not authorized to upload evidence for this dispute.");

            // Upload to Cloudinary
            var fileUrl = await _fileUploadService.UploadFileAsync(file, "disputes");

            var evidence = new DisputeEvidence
            {
                RentalDepositId = deposit.Id,
                Url = fileUrl,
                SubmittedByUserId = userId,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.DisputeEvidences.Add(evidence);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Evidence uploaded for rental {RentalId} by user {UserId}. URL: {Url}",
                rentalId, userId, fileUrl);

            return evidence;
        }
    }
}
