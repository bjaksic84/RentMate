namespace RentMate.Services.Interfaces
{
    /// <summary>
    /// Abstraction for payment operations. Supports future integration
    /// with multiple payment providers (Stripe, PayPal, etc.).
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Authorizes (holds) an amount on the user's payment method without charging.
        /// </summary>
        Task<PaymentResult> AuthorizeAsync(string userId, decimal amount, string description);

        /// <summary>
        /// Captures (charges) a previously authorized amount.
        /// </summary>
        Task<PaymentResult> CaptureAsync(string paymentReference, decimal amount);

        /// <summary>
        /// Releases a previously authorized hold.
        /// </summary>
        Task<PaymentResult> ReleaseAsync(string paymentReference);

        /// <summary>
        /// Refunds a previously captured payment.
        /// </summary>
        Task<PaymentResult> RefundAsync(string paymentReference, decimal amount);
    }

    /// <summary>
    /// Result of a payment operation.
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string? PaymentReference { get; set; }
        public string? ErrorMessage { get; set; }

        public static PaymentResult Succeeded(string paymentReference) => new()
        {
            Success = true,
            PaymentReference = paymentReference
        };

        public static PaymentResult Failed(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
