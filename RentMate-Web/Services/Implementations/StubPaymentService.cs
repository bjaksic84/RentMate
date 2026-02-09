using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations
{
    /// <summary>
    /// Stub payment service that logs operations and always succeeds.
    /// Replace with a real provider (Stripe, PayPal, etc.) when payment integration is implemented.
    /// </summary>
    public class StubPaymentService : IPaymentService
    {
        private readonly ILogger<StubPaymentService> _logger;

        public StubPaymentService(ILogger<StubPaymentService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<PaymentResult> AuthorizeAsync(string userId, decimal amount, string description)
        {
            var reference = $"STUB-AUTH-{Guid.NewGuid():N}";
            _logger.LogInformation(
                "STUB: Authorized {Amount:C} for user {UserId}: {Description}. Ref: {Reference}",
                amount, userId, description, reference);
            return Task.FromResult(PaymentResult.Succeeded(reference));
        }

        /// <inheritdoc/>
        public Task<PaymentResult> CaptureAsync(string paymentReference, decimal amount)
        {
            _logger.LogInformation(
                "STUB: Captured {Amount:C} from authorization {Reference}",
                amount, paymentReference);
            return Task.FromResult(PaymentResult.Succeeded(paymentReference));
        }

        /// <inheritdoc/>
        public Task<PaymentResult> ReleaseAsync(string paymentReference)
        {
            _logger.LogInformation(
                "STUB: Released authorization {Reference}",
                paymentReference);
            return Task.FromResult(PaymentResult.Succeeded(paymentReference));
        }

        /// <inheritdoc/>
        public Task<PaymentResult> RefundAsync(string paymentReference, decimal amount)
        {
            _logger.LogInformation(
                "STUB: Refunded {Amount:C} for {Reference}",
                amount, paymentReference);
            return Task.FromResult(PaymentResult.Succeeded(paymentReference));
        }
    }
}
