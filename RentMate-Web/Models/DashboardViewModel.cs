using System.Collections.Generic;

namespace RentMate.Models
{
    public class DashboardViewModel
    {
        // Admin metrics (kept for AdminDashboard)
        public int TotalUsers { get; set; }
        public int TotalListings { get; set; }
        public int ActiveListings { get; set; }
        public int TotalRentals { get; set; }
        public int ActiveRentals { get; set; }

        // Shared lists used by admin or debugging
        public List<ApplicationUser>? Users { get; set; }
        public List<Item>? Listings { get; set; }
        public List<Rental>? Rentals { get; set; }

        // --- User dashboard specifics ---
        // Items this user owns
        public List<Item>? ListingsOwned { get; set; }

        // Rentals for items this user owns (owner's perspective)
        public List<Rental>? OwnerRentals { get; set; }

        // Rentals where this user is the renter (renter's perspective)
        public List<Rental>? MyRentals { get; set; }

        // quick summary counts
        public int TotalListingsOwned { get; set; }
        public int ActiveListingsOwned { get; set; }
        public int TotalRentalsAsRenter { get; set; }
        public int TotalRentalsAsOwner { get; set; }
    }
}



