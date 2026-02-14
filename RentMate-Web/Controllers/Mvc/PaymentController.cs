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
using RentMate.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using RentMate.Hubs;

namespace RentMate.Controllers.Mvc
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<PaymentController> _localizer;
        private readonly ILogger<PaymentController> _logger;
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly IRentalExtensionService _extensionService;
        private readonly IDepositService _depositService;
        private readonly IHubContext<RentMateHub> _hubContext;

        public PaymentController(
            RentMateContext context,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<PaymentController> localizer,
            ILogger<PaymentController> logger,
            IPaymentService paymentService,
            IConfiguration configuration,
            IRentalExtensionService extensionService,
            IDepositService depositService,
            IHubContext<RentMateHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
            _logger = logger;
            _paymentService = paymentService;
            _configuration = configuration;
            _extensionService = extensionService;
            _depositService = depositService;
            _hubContext = hubContext;
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

            // Business rule: Can only pay for Accepted rentals
            if (rental.Status != RentalStatus.Accepted)
            {
                TempData["Error"] = _localizer["This rental must be accepted by the owner before payment."].Value;
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

            var clientSecret = "";
            var publishableKey = _configuration["Stripe:PublishableKey"];

            try
            {
                // Create PaymentIntent
                var result = await _paymentService.AuthorizeAsync(userId, rental.TotalPrice, $"Rental Payment for {rental.Item.Title} (Rental ID: {rental.Id})");
                
                if (!result.Success)
                {
                     TempData["Error"] = _localizer["Failed to initialize payment: "].Value + result.ErrorMessage;
                     return RedirectToAction("MyRentals", "Rentals");
                }
                
                clientSecret = result.ClientSecret;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment intent for rental {RentalId}", rentalId);
                 TempData["Error"] = _localizer["An unexpected error occurred."].Value;
                 return RedirectToAction("MyRentals", "Rentals");
            }

            ViewBag.ClientSecret = clientSecret;
            ViewBag.PublishableKey = publishableKey;
            
            return View(rental);
        }

        [HttpGet]
        public async Task<IActionResult> Success(int rentalId, string payment_intent, string payment_intent_client_secret, string redirect_status)
        {
             var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null)
                return NotFound();
            
            if (redirect_status == "succeeded")
            {
                 // Verify if we already recorded this payment
                 var existingPayment = await _context.Payments
                    .AnyAsync(p => p.RentalId == rentalId && p.Status == PaymentStatus.Success);

                 if (!existingPayment)
                 {
                     // Record successful payment
                     var payment = new Payment
                    {
                        RentalId = rentalId,
                        UserId = userId,
                        Amount = rental.TotalPrice,
                        Status = PaymentStatus.Success,
                        TransactionId = payment_intent,
                        CreatedAt = DateTime.UtcNow
                    };

                    rental.Status = RentalStatus.Active;
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Create deposit hold if item requires a deposit
                    if (rental.Item?.DepositAmount is > 0)
                    {
                        try
                        {
                            await _depositService.CreateAndAuthorizeDepositAsync(rentalId, rental.Item.DepositAmount.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Deposit authorization failed for rental {RentalId}. Deposit will need manual resolution.", rentalId);
                        }
                    }
                 }
                
                return View(rental);
            }
            else
            {
                TempData["Error"] = "Payment failed or was cancelled.";
                return RedirectToAction("Checkout", new { rentalId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExtensionCheckout(int extensionId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var extension = await _context.RentalExtensions
                .Include(e => e.Rental)
                    .ThenInclude(r => r!.Item)
                .FirstOrDefaultAsync(e => e.Id == extensionId);

            if (extension == null)
                return NotFound();

            if (extension.Rental?.RenterId != userId)
                return Forbid();

            if (extension.Status != Models.Domain.ExtensionStatus.Accepted && extension.Status != Models.Domain.ExtensionStatus.AutoApproved)
            {
                TempData["Error"] = _localizer["This extension must be accepted or auto-approved before payment."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            var clientSecret = "";
            var publishableKey = _configuration["Stripe:PublishableKey"];

            try
            {
                var result = await _paymentService.AuthorizeAsync(userId, extension.AdditionalCost,
                    $"Extension Payment for {extension.Rental!.Item!.Title} (Extension ID: {extension.Id})");

                if (!result.Success)
                {
                    TempData["Error"] = _localizer["Failed to initialize payment: "].Value + result.ErrorMessage;
                    return RedirectToAction("UserDashboard", "Dashboard");
                }

                clientSecret = result.ClientSecret;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment intent for extension {ExtensionId}", extensionId);
                TempData["Error"] = _localizer["An unexpected error occurred."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            ViewBag.ClientSecret = clientSecret;
            ViewBag.PublishableKey = publishableKey;
            ViewBag.IsExtension = true;
            ViewBag.ExtensionId = extensionId;
            ViewBag.ExtensionCost = extension.AdditionalCost;
            ViewBag.ExtensionNewEndDate = extension.NewEndDate;

            return View("Checkout", extension.Rental);
        }

        [HttpGet]
        public async Task<IActionResult> ExtensionSuccess(int extensionId, string payment_intent, string payment_intent_client_secret, string redirect_status)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var extension = await _context.RentalExtensions
                .Include(e => e.Rental)
                    .ThenInclude(r => r!.Item)
                .FirstOrDefaultAsync(e => e.Id == extensionId);

            if (extension == null)
                return NotFound();

            if (redirect_status == "succeeded")
            {
                if (extension.Status == Models.Domain.ExtensionStatus.Accepted || extension.Status == Models.Domain.ExtensionStatus.AutoApproved)
                {
                    // Record payment
                    var payment = new Payment
                    {
                        RentalId = extension.RentalId,
                        UserId = userId,
                        Amount = extension.AdditionalCost,
                        Status = PaymentStatus.Success,
                        TransactionId = payment_intent,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Finalize the extension (update dates + price)
                    await _extensionService.FinalizeExtensionAsync(extensionId, userId);

                    // Notify the owner
                    if (extension.Rental?.OwnerId != null)
                    {
                        await _hubContext.Clients.User(extension.Rental.OwnerId).SendAsync(
                            RentMateHub.ExtensionStatusChangedEvent, new
                            {
                                extensionId,
                                status = "Paid",
                                itemTitle = extension.Rental.Item?.Title,
                                newEndDate = extension.NewEndDate.ToString("yyyy-MM-dd")
                            });
                    }
                }

                return View("Success", extension.Rental);
            }
            else
            {
                TempData["Error"] = "Payment failed or was cancelled.";
                return RedirectToAction("ExtensionCheckout", new { extensionId });
            }
        }
    }
}

