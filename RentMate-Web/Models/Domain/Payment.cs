using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    public enum PaymentStatus { Pending, Success, Failed }

    /// <summary>
    /// Represents a payment transaction in the RentMate system.
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the rental this payment is for.
        /// </summary>
        public int RentalId { get; set; }

        /// <summary>
        /// Foreign key to the user who made the payment.
        /// </summary>
        public string? UserId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public string TransactionId { get; set; } = Guid.NewGuid().ToString();
        public PaymentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties for Entity Framework
        public virtual Rental? Rental { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
