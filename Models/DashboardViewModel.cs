using System.Collections.Generic;

namespace RentMate.Models
{
    public class DashboardViewModel
    {
        // --- Admin Metrics ---
        public int TotalUsers { get; set; }
        public int TotalListings { get; set; }
        public int ActiveListings { get; set; }
        public int TotalRentals { get; set; }
        public int ActiveRentals { get; set; }

        // --- Shared Collections ---
        public List<ApplicationUser>? Users { get; set; }
        public List<Item>? Listings { get; set; }
        public List<Rental>? Rentals { get; set; }

        // --- User Dashboard Specific ---
        public List<Rental>? OwnerRentals { get; set; }   // Rentals of my items (as owner)
        public List<Rental>? MyRentals { get; set; }      // Rentals I made (as renter)
    }
}



