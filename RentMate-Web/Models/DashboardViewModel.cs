namespace RentMate.Models
{
    /// <summary>
    /// Dashboard view model for MVC views.
    /// Contains both admin and user dashboard data.
    /// </summary>
    public class DashboardViewModel
    {
        // Admin statistics
        public int TotalUsers { get; set; }
        public int TotalListings { get; set; }
        public int ActiveListings { get; set; }
        public int TotalRentals { get; set; }
        public int ActiveRentals { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }

        // User-specific statistics
        public int TotalListingsOwned { get; set; }
        public int ActiveListingsOwned { get; set; }
        public int TotalRentalsAsRenter { get; set; }
        public int TotalRentalsAsOwner { get; set; }
        public int ActiveRentalsAsRenter { get; set; }
        public int ActiveRentalsAsOwner { get; set; }

        // Shared lists for admin/debugging
        public List<ApplicationUser>? Users { get; set; }
        public List<Item>? Listings { get; set; }
        public List<Rental>? Rentals { get; set; }
        public List<Payment>? RecentPayments { get; set; }

        // User dashboard specifics
        /// <summary>Items this user owns.</summary>
        public List<Item>? ListingsOwned { get; set; }

        /// <summary>Rentals for items this user owns (owner's perspective).</summary>
        public List<Rental>? OwnerRentals { get; set; }

        /// <summary>Rentals where this user is the renter (renter's perspective).</summary>
        public List<Rental>? MyRentals { get; set; }
    }
}