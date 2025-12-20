namespace RentMate.Shared
{
    public class PaymentDto : Payment
    {
        public UserDto? User { get; set; }

        public RentalDto? Rental { get; set; } 
    }
}