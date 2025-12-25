using System.Collections.Generic;

namespace RentMate.Shared
{
    public class DashboardViewModel
    {
        // Admin metrics (kept for AdminDashboard)
        public int TotalUsers { get; set; }
        public int TotalListings { get; set; }
        public int ActiveListings { get; set; }
        public int TotalRentals { get; set; }
        public int ActiveRentals { get; set; }

        // NOVO: Finančni podatki za Admina
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        

        // quick summary counts
        public int TotalListingsOwned { get; set; }
        public int ActiveListingsOwned { get; set; }
        public int TotalRentalsAsRenter { get; set; }
        public int TotalRentalsAsOwner { get; set; }
    }
}



