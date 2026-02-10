namespace RentMate.Shared.Contracts.Responses
{
    public class PaymentIntentResponse
    {
        public string? ClientSecret { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? PublishableKey { get; set; }
    }
}
