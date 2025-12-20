using RentMate.Shared;

namespace RentMate.Shared
{
    public enum PaymentStatus { Pending, Success, Failed }

    public class Payment
    {
        public int Id { get; set; }
        public int RentalId { get; set; }
        

        // Neposredna povezava do uporabnika, ki je opravil plačilo
        public string? UserId { get; set; }
        

        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = Guid.NewGuid().ToString();
        public PaymentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}