using Stripe;
using RentMate.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RentMate.Services.Implementations
{
    public class StripePaymentService : IPaymentService
    {
        private readonly ILogger<StripePaymentService> _logger;
        private readonly string _publishableKey;
        private readonly string _secretKey;

        public StripePaymentService(
            IConfiguration configuration,
            ILogger<StripePaymentService> logger)
        {
            _logger = logger;
            _publishableKey = configuration["Stripe:PublishableKey"] 
                ?? throw new InvalidOperationException("Stripe PublishableKey is missing.");
            _secretKey = configuration["Stripe:SecretKey"] 
                ?? throw new InvalidOperationException("Stripe SecretKey is missing.");

            StripeConfiguration.ApiKey = _secretKey;
        }

        public async Task<PaymentResult> AuthorizeAsync(string userId, decimal amount, string description)
        {
            try
            {
                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100), // Convert to cents
                    Currency = "eur", // Default to EUR for now, could be passed in
                    PaymentMethodTypes = new List<string> { "card" },
                    Description = description,
                    CaptureMethod = "manual", // Authorize only
                    Metadata = new Dictionary<string, string>
                    {
                        { "UserId", userId }
                    }
                };

                var service = new PaymentIntentService();
                var intent = await service.CreateAsync(options);

                return PaymentResult.Succeeded(intent.Id, intent.ClientSecret);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe Authorization failed: {Message}", ex.Message);
                return PaymentResult.Failed(ex.Message);
            }
        }

        public async Task<PaymentResult> CaptureAsync(string paymentReference, decimal amount)
        {
            try
            {
                var service = new PaymentIntentService();
                
                var options = new PaymentIntentCaptureOptions
                {
                    AmountToCapture = (long)(amount * 100)
                };

                var intent = await service.CaptureAsync(paymentReference, options);
                
                if (intent.Status == "succeeded")
                {
                    return PaymentResult.Succeeded(intent.Id);
                }
                
                return PaymentResult.Failed($"Payment capture status: {intent.Status}");
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe Capture failed: {Message}", ex.Message);
                return PaymentResult.Failed(ex.Message);
            }
        }

        public async Task<PaymentResult> ReleaseAsync(string paymentReference)
        {
            try
            {
                var service = new PaymentIntentService();
                var intent = await service.CancelAsync(paymentReference);
                
                if (intent.Status == "canceled")
                {
                    return PaymentResult.Succeeded(intent.Id);
                }
                
                return PaymentResult.Failed($"Payment release status: {intent.Status}");
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe Release failed: {Message}", ex.Message);
                return PaymentResult.Failed(ex.Message);
            }
        }

        public async Task<PaymentResult> RefundAsync(string paymentReference, decimal amount)
        {
            try
            {
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = paymentReference,
                    Amount = (long)(amount * 100)
                };
                
                var service = new RefundService();
                var refund = await service.CreateAsync(refundOptions);

                if (refund.Status == "succeeded" || refund.Status == "pending")
                {
                    return PaymentResult.Succeeded(refund.Id);
                }

                return PaymentResult.Failed($"Refund status: {refund.Status}");
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe Refund failed: {Message}", ex.Message);
                return PaymentResult.Failed(ex.Message);
            }
        }
    }
}
