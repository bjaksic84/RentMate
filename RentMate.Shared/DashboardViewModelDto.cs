namespace RentMate.Shared
{
    public class DashboardViewModelDto : DashboardViewModel
    {
        public List<Payment>? RecentPayments { get; set; }

        // Shared lists used by admin or debugging
        public List<UserDto>? Users { get; set; }
        public List<Item>? Listings { get; set; }
        public List<Rental>? Rentals { get; set; }

        // --- User dashboard specifics ---
        // Items this user owns
        public List<Item>? ListingsOwned { get; set; }

        // Rentals for items this user owns (owner's perspective)
        public List<Rental>? OwnerRentals { get; set; }

        // Rentals where this user is the renter (renter's perspective)
        public List<Rental>? MyRentals { get; set; }
        
    }
}