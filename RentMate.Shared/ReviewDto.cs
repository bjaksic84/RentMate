namespace RentMate.Shared
{
    public class ReviewDto : Review
    {
        public UserDto? Reviewer { get; set; }
        public RentalDto? Rental { get; set; }

        public ItemDto? Item { get; set; }
    }
    
}