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

        // ── Payment-method management (Stripe SetupIntent flow) ─────

        /// <summary>
        /// Creates a Stripe SetupIntent so the user can save a payment method.
        /// If stripeCustomerId is provided, the payment method will be attached to that customer.
        /// for future charges. Returns a ClientSecret for Stripe Elements.
        /// </summary>
        Task<PaymentResult> CreateSetupIntentAsync(string userId, string? stripeCustomerId = null);

        /// <summary>
        /// Lists the saved payment methods (cards) for a Stripe customer.
        /// Returns a list of <see cref="SavedPaymentMethod"/> DTOs.
        /// </summary>
        Task<IReadOnlyList<SavedPaymentMethod>> ListPaymentMethodsAsync(string stripeCustomerId);

        /// <summary>
        /// Detaches (removes) a payment method from the customer.
        /// </summary>
        Task<PaymentResult> RemovePaymentMethodAsync(string paymentMethodId);

        /// <summary>
        /// Gets or creates a Stripe Customer for the given user.
        /// </summary>
        Task<string> GetOrCreateCustomerAsync(string userId, string email, string? name = null);

        /// <summary>
        /// Deletes the Stripe Customer associated with the given email,
        /// including all attached payment methods. No-op if no customer exists.
        /// </summary>
        Task DeleteCustomerAsync(string email);
    }

    /// <summary>
    /// Lightweight view of a saved payment method (card).
    /// </summary>
    public class SavedPaymentMethod
    {
        public string Id { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;
        public long ExpMonth { get; set; }
        public long ExpYear { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// Result of a payment operation.
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string? PaymentReference { get; set; }
        public string? ClientSecret { get; set; }
        public string? ErrorMessage { get; set; }

        public static PaymentResult Succeeded(string paymentReference, string? clientSecret = null) => new()
        {
            Success = true,
            PaymentReference = paymentReference,
            ClientSecret = clientSecret
        };

        public static PaymentResult Failed(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
