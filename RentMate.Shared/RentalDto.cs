namespace RentMate.Shared
{
    public class RentalDto : Rental
    {
        public UserDto? Owner { get; set; }
        public UserDto? Renter { get; set; }
        public ItemDto? Item { get; set; }
        public ReviewDto? ExistingReview { get; set; }
    }
}