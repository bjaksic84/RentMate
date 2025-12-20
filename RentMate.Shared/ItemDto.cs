// RentMate.Shared/Models/ItemDto.cs
namespace RentMate.Shared
{
    public class ItemDto : Item
    {
        public UserDto? User { get; set; }

        public ICollection<RentalDto>? Rentals { get; set; }

        public ICollection<ReviewDto>? Reviews { get; set; }
    }
}