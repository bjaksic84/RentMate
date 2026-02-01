using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Controllers.Mvc
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<PaymentController> _localizer;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            RentMateContext context, 
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<PaymentController> localizer,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int rentalId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null)
                return NotFound(_localizer["Rental not found."].Value);

            // Authorization: Only the renter can access the payment page
            if (rental.RenterId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to access payment for rental {RentalId} belonging to {RenterId}",
                    userId, rentalId, rental.RenterId);
                return Forbid();
            }

            // Business rule: Can only pay for pending rentals
            if (rental.Status != RentalStatus.Pending)
            {
                TempData["Error"] = _localizer["This rental cannot be paid for in its current status."].Value;
                return RedirectToAction("MyRentals", "Rentals");
            }

            // Check if already paid
            var existingPayment = await _context.Payments
                .AnyAsync(p => p.RentalId == rentalId && p.Status == PaymentStatus.Success);
            
            if (existingPayment)
            {
                TempData["Info"] = _localizer["This rental has already been paid."].Value;
                return RedirectToAction("MyRentals", "Rentals");
            }

            return View(rental);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int rentalId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null)
                return NotFound(_localizer["Rental not found."].Value);

            // Authorization: Only the renter can pay
            if (rental.RenterId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to pay for rental {RentalId} belonging to {RenterId}",
                    userId, rentalId, rental.RenterId);
                return Forbid();
            }

            // Business rule: Can only pay for pending rentals
            if (rental.Status != RentalStatus.Pending)
            {
                TempData["Error"] = _localizer["This rental cannot be paid for in its current status."].Value;
                return RedirectToAction("Checkout", new { rentalId });
            }

            // Check for duplicate payment
            var existingPayment = await _context.Payments
                .AnyAsync(p => p.RentalId == rentalId && p.Status == PaymentStatus.Success);
            
            if (existingPayment)
            {
                TempData["Error"] = _localizer["This rental has already been paid."].Value;
                return RedirectToAction("MyRentals", "Rentals");
            }

            // TODO: Integrate with actual payment provider (Stripe, PayPal, etc.)
            // The current implementation simulates a successful payment.
            // In production, you would:
            // 1. Create a payment intent with your payment provider
            // 2. Redirect to their hosted checkout or use their SDK
            // 3. Handle the webhook callback to confirm payment
            // NEVER handle card numbers directly - use tokenized payments (PCI DSS compliance)

            var payment = new Payment
            {
                RentalId = rentalId,
                UserId = userId,
                Amount = rental.TotalPrice,
                Status = PaymentStatus.Success,
                TransactionId = $"SIM-{Guid.NewGuid():N}", // Simulated transaction ID
                CreatedAt = DateTime.UtcNow
            };

            // Update rental status to Active after successful payment
            rental.Status = RentalStatus.Active;

            _context.Payments.Add(payment);
            
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Payment {PaymentId} processed for rental {RentalId} by user {UserId}. Amount: {Amount}",
                    payment.Id, rentalId, userId, payment.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save payment for rental {RentalId}", rentalId);
                TempData["Error"] = _localizer["Payment processing failed. Please try again."].Value;
                return RedirectToAction("Checkout", new { rentalId });
            }

            TempData["Success"] = _localizer["Payment successful!"].Value;
            return View("Success", payment);
        }
    }
}

