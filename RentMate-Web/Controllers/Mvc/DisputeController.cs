using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Infrastructure.Queries;
using RentMate.Models.Domain;
using RentMate.Controllers.Base;
using RentMate.Services.Interfaces;

namespace RentMate.Controllers.Mvc
{
    /// <summary>
    /// MVC Controller for deposit and dispute actions.
    /// Handles deposit release/charge, dispute filing, counter-offers, escalation, and admin resolution.
    /// </summary>
    [Authorize]
    public class DisputeController : BaseAppController
    {
        #region Constants

        private const string AdminRole = "Admin";

        #endregion

        #region Dependencies

        private readonly RentMateContext _context;
        private readonly IDepositService _depositService;
        private readonly INotificationDispatcher _dispatcher;
        private readonly ILogger<DisputeController> _logger;

        #endregion

        #region Constructor

        public DisputeController(
            UserManager<ApplicationUser> userManager,
            RentMateContext context,
            IDepositService depositService,
            INotificationDispatcher dispatcher,
            ILogger<DisputeController> logger)
            : base(userManager)
        {
            _context = context;
            _depositService = depositService;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns a safe error message for the client. Only forwards messages from expected
        /// exception types; unexpected exceptions get a generic message to prevent information leakage.
        /// </summary>
        private static string SafeErrorMessage(Exception ex) => ex switch
        {
            InvalidOperationException => ex.Message,
            UnauthorizedAccessException => ex.Message,
            ArgumentException => ex.Message,
            _ => "An unexpected error occurred. Please try again."
        };

        #endregion

        #region Deposit Actions

        /// <summary>
        /// Releases the deposit for a completed rental (owner action).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseDeposit(int rentalId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.ReleaseDepositAsync(rentalId, user.Id);

                var relRental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (relRental != null)
                    await _dispatcher.DepositReleasedAsync(rentalId, relRental.RenterId!, relRental.Item?.Title);

                return Json(new { success = true, message = "Deposit released." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Deposit release failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Charges a partial or full deposit amount (owner action).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChargeDeposit(int rentalId, decimal amount, string reason, IFormFile? evidence)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            if (evidence == null || evidence.Length == 0)
            {
                return Json(new { success = false, message = "Picture evidence is required for all deposit charges." });
            }

            try
            {
                await _depositService.ChargeDepositAsync(rentalId, amount, reason, user.Id);

                // Upload evidence immediately
                await _depositService.UploadEvidenceAsync(rentalId, user.Id, evidence, reason);

                var chgRental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (chgRental != null)
                    await _dispatcher.DepositChargedAsync(rentalId, chgRental.RenterId!, chgRental.Item?.Title, amount, reason);

                return Json(new { success = true, message = "Deposit charged with evidence." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Deposit charge failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Releases a disputed deposit back to the renter (owner action).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseDisputedDeposit(int rentalId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.ReleaseDisputedDepositAsync(rentalId, user.Id);

                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental != null)
                    await _dispatcher.DepositReleasedAsync(rentalId, rental.RenterId!, rental.Item?.Title);

                return Json(new { success = true, message = "Disputed deposit released." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Disputed deposit release failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Owner completes a rental and either releases or charges the deposit.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteWithDeposit(int rentalId, string action, decimal? amount, string? reason, IFormFile? evidence)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            if ((action == "charge" || action == "charge-full") && (evidence == null || evidence.Length == 0))
            {
                return Json(new { success = false, message = "Picture evidence is required for all deposit charges." });
            }

            try
            {
                var rental = await _context.Rentals.Include(r => r.Deposit).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental == null) return Json(new { success = false, message = "Rental not found." });

                if (action == "release")
                {
                    await _depositService.ReleaseDepositAsync(rentalId, user.Id);
                }
                else if (action == "charge")
                {
                    if (!amount.HasValue || amount <= 0)
                        return Json(new { success = false, message = "Invalid charge amount." });

                    await _depositService.ChargeDepositAsync(rentalId, amount.Value, reason ?? "Early Return Charge", user.Id);

                    if (evidence != null)
                    {
                        await _depositService.UploadEvidenceAsync(rentalId, user.Id, evidence, reason ?? "Early Return Charge");
                    }
                }
                else if (action == "charge-full")
                {
                    if (rental.Deposit == null)
                        return Json(new { success = false, message = "No deposit found." });

                    await _depositService.ChargeDepositAsync(rentalId, rental.Deposit.Amount, reason ?? "Full Deposit Charge", user.Id);

                    if (evidence != null)
                    {
                        await _depositService.UploadEvidenceAsync(rentalId, user.Id, evidence, reason ?? "Full Deposit Charge");
                    }
                }

                if (rental != null)
                {
                    if (action == "release")
                        await _dispatcher.DepositReleasedAsync(rentalId, rental.RenterId!, rental.Item?.Title);
                    else
                        await _dispatcher.DepositChargedAsync(rentalId, rental.RenterId!, rental.Item?.Title, amount, reason);
                }

                return Json(new { success = true, message = $"Rental completed and deposit {action}d." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Complete with deposit failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        #endregion

        #region Dispute Actions

        /// <summary>
        /// Disputes a deposit charge (renter action).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisputeDeposit(int rentalId, string reason)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.DisputeDepositAsync(rentalId, reason, user.Id);

                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental != null)
                    await _dispatcher.DepositDisputedAsync(rentalId, rental.OwnerId!, rental.Item?.Title);

                return Json(new { success = true, message = "Deposit disputed." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Deposit dispute failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Renter accepts a deposit charge (no dispute).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptCharge(int rentalId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.AcceptChargeAsync(rentalId, user.Id);

                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental != null)
                    await _dispatcher.DepositResolvedAsync(rentalId, rental.OwnerId!, rental.Item?.Title);

                return Json(new { success = true, message = "Charge accepted." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Accept charge failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Renter accepts a counter-offer from the owner.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptCounterOffer(int rentalId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.AcceptCounterOfferAsync(rentalId, user.Id);

                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental != null)
                    await _dispatcher.DepositResolvedAsync(rentalId, rental.OwnerId!, rental.Item?.Title, "CounterAccepted");

                return Json(new { success = true, message = "Counter-offer accepted." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Accept counter-offer failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Renter rejects a counter-offer from the owner.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCounterOffer(int rentalId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.RejectCounterOfferAsync(rentalId, user.Id);

                var rental = await _context.Rentals.Include(r => r.Item).Include(r => r.Deposit).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental != null)
                    await _dispatcher.DepositCounterRejectedAsync(rentalId, rental.OwnerId!, rental.Item?.Title,
                        escalated: rental.Deposit?.Status == DepositStatus.Escalated);

                return Json(new { success = true, message = "Counter-offer rejected. Original charge stands." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reject counter-offer failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Owner makes a counter-offer on a disputed deposit.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CounterOfferDeposit(int rentalId, decimal amount, string response, IFormFile? evidence)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.CounterOfferDepositAsync(rentalId, amount, response, user.Id);

                // Upload evidence if provided
                if (evidence != null && evidence.Length > 0)
                {
                    await _depositService.UploadEvidenceAsync(rentalId, user.Id, evidence, response);
                }

                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental != null)
                    await _dispatcher.DepositCounterOfferedAsync(rentalId, rental.RenterId!, rental.Item?.Title);

                return Json(new { success = true, message = "Counter-offer sent with evidence." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Counter-offer failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Either party escalates the dispute to admin intervention.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EscalateDispute(int rentalId, string? response = null, IFormFile? evidence = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental == null)
                    return Json(new { success = false, message = "Rental not found." });

                await _depositService.EscalateDisputeAsync(rentalId, user.Id, response);

                if (evidence != null && evidence.Length > 0)
                {
                    await _depositService.UploadEvidenceAsync(rentalId, user.Id, evidence, response ?? "Escalation Evidence");
                }

                // Notify the party that did NOT escalate
                string? recipientId = (user.Id == rental.OwnerId) ? rental.RenterId : rental.OwnerId;

                if (!string.IsNullOrEmpty(recipientId))
                    await _dispatcher.DepositEscalatedAsync(rentalId, recipientId, rental.Item?.Title);

                return Json(new { success = true, message = "Dispute escalated to administration." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Escalate dispute failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        /// <summary>
        /// Owner maintains charge (alias for EscalateDispute).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MaintainCharge(int rentalId, string? response = null, IFormFile? evidence = null)
        {
            return await EscalateDispute(rentalId, response, evidence);
        }

        #endregion

        #region Evidence

        /// <summary>
        /// Uploads dispute evidence for a rental deposit.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDisputeEvidence(int rentalId, IFormFile file, string? notes)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            try
            {
                var evidence = await _depositService.UploadEvidenceAsync(rentalId, user.Id, file, notes);
                return Json(new {
                    success = true,
                    message = "Evidence uploaded successfully.",
                    url = evidence.Url,
                    id = evidence.Id,
                    submittedBy = user.UserName,
                    createdAt = evidence.CreatedAt.ToString("g")
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Evidence upload failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        #endregion

        #region Admin Actions

        /// <summary>
        /// Admin resolves an escalated deposit dispute.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AdminRole)]
        public async Task<IActionResult> AdminResolveDispute(int rentalId, decimal amount, string? adminNotes)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            try
            {
                await _depositService.AdminResolveDisputeAsync(rentalId, amount, adminNotes ?? "", user.Id);

                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == rentalId);
                if (rental != null)
                    await _dispatcher.DepositAdminResolvedAsync(rentalId, rental.OwnerId!, rental.RenterId!, rental.Item?.Title, amount, adminNotes);

                return Json(new { success = true, message = amount == 0 ? "Deposit released to renter." : $"Charge finalized at \u20ac{amount:N2}." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Admin resolve dispute failed for rental {RentalId}", rentalId);
                return Json(new { success = false, message = SafeErrorMessage(ex) });
            }
        }

        #endregion

        #region Views

        /// <summary>
        /// Displays the dispute review page for admins.
        /// </summary>
        [Authorize(Roles = AdminRole)]
        public async Task<IActionResult> AdminReviewDispute(int id)
        {
            var rental = await _context.Rentals
                .WithDisputeDetails()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rental == null || rental.Deposit == null || rental.Deposit.Status != DepositStatus.Escalated)
            {
                return RedirectToAction("AdminDashboard", "Dashboard");
            }

            return View(rental);
        }

        /// <summary>
        /// Displays the list of resolved disputes for admins.
        /// </summary>
        [Authorize(Roles = AdminRole)]
        public async Task<IActionResult> AdminResolvedDisputes()
        {
            var resolvedDisputes = await _depositService.GetResolvedDisputesAsync();
            return View(resolvedDisputes);
        }

        /// <summary>
        /// Displays the dispute history timeline for a rental.
        /// </summary>
        [Authorize]
        public async Task<IActionResult> DisputeHistory(int id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole(AdminRole);

            var rental = await _context.Rentals
                .WithDisputeDetails()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rental == null || rental.Deposit == null)
            {
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            // Authorization check
            if (!isAdmin && rental.RenterId != userId && rental.OwnerId != userId)
            {
                return Forbid();
            }

            return View(rental);
        }

        #endregion
    }
}
