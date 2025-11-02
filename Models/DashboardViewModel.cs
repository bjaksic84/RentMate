using RentMate.Models;

namespace RentMate.Models
{
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalListings { get; set; }
        public int ActiveListings { get; set; }
        public int TotalRentals { get; set; }

        public List<ApplicationUser>? Users { get; set; }
        public List<Item>? Listings { get; set; }
        public List<Rental>? Rentals { get; set; }
    }
}


