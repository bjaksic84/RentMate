using System.ComponentModel.DataAnnotations;

namespace RentMate.Shared.Contracts.Requests
{
    public class CreatePaymentIntentRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        public string Currency { get; set; } = "eur";

        public string? PaymentMethodId { get; set; }
        
        /// <summary>
        /// Optional: ID of the item/rental being paid for, for metadata.
        /// </summary>
        public int? RentalId { get; set; }
    }
}
