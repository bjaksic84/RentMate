using Microsoft.AspNetCore.Mvc;
using Stripe;
using RentMate.Services.Interfaces;
using RentMate.Shared.Contracts.Requests;
using RentMate.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RentMate.Models.Domain;

namespace RentMate.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;
        private readonly string? _webhookSecret;
        private readonly RentMate.Infrastructure.Data.RentMateContext _context;

        public PaymentController(
            IPaymentService paymentService,
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            RentMate.Infrastructure.Data.RentMateContext context)
        {
            _paymentService = paymentService;
            _logger = logger;
            _webhookSecret = configuration["Stripe:WebhookSecret"];
            _context = context;
        }

        [HttpPost("create-intent")]
        [Authorize]
        public async Task<ActionResult<PaymentIntentResponse>> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // In a real app, validate that the user can pay for this specific RentalId
            // and that the amount matches the rental cost.
            
            var result = await _paymentService.AuthorizeAsync(userId, request.Amount, $"Rental Payment: {request.RentalId}");

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(new PaymentIntentResponse
            {
                ClientSecret = result.ClientSecret,
                PaymentIntentId = result.PaymentReference
            });
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _webhookSecret
                );

                // Handle the event
                if (stripeEvent.Type == Stripe.EventTypes.PaymentIntentSucceeded)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    _logger.LogInformation("PaymentIntent succeeded: {Id}", paymentIntent?.Id);
                    
                    if (paymentIntent != null && paymentIntent.Metadata.TryGetValue("UserId", out var userId))
                    {
                        var rentalIdStr = paymentIntent.Description.Split("Rental ID: ").Last().Trim(')');
                        if (int.TryParse(rentalIdStr, out var rentalId))
                        {
                            var rental = await _context.Rentals.FirstOrDefaultAsync(r => r.Id == rentalId);
                            if (rental != null)
                            {
                                var existingPayment = await _context.Payments
                                    .AnyAsync(p => p.TransactionId == paymentIntent.Id && p.Status == PaymentStatus.Success);
                                
                                if (!existingPayment)
                                {
                                    var payment = new Payment
                                    {
                                        RentalId = rentalId,
                                        UserId = userId,
                                        Amount = (decimal)paymentIntent.Amount / 100m,
                                        Status = PaymentStatus.Success,
                                        TransactionId = paymentIntent.Id,
                                        CreatedAt = DateTime.UtcNow
                                    };

                                    rental.Status = RentalStatus.Active;
                                    _context.Payments.Add(payment);
                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("Payment recorded via Webhook for Rental {RentalId}", rentalId);
                                }
                            }
                        }
                    }
                }
                else if (stripeEvent.Type == Stripe.EventTypes.PaymentIntentPaymentFailed)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    _logger.LogWarning("PaymentIntent failed: {Id}", paymentIntent?.Id);
                }

                return Ok();
            }
            catch (StripeException e)
            {
                if (string.IsNullOrEmpty(_webhookSecret))
                {
                    // If webhook secret is not configured (dev mode without secret), 
                    // we might want to just log the raw event for debugging or ignore signature validation
                    // But standard Stripe.net usage throws if signature verification fails.
                    _logger.LogWarning("Webhook received but Secret not configured or verification failed: {Message}", e.Message);
                    return BadRequest();
                }
                
                _logger.LogError(e, "Stripe Webhook Error");
                return BadRequest();
            }
            catch (Exception e)
            {
                 _logger.LogError(e, "General Webhook Error");
                return StatusCode(500);
            }
        }
    }
}
