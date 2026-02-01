using RentMate.Models.Domain;

namespace RentMate.Models.ViewModels;

/// <summary>
/// Dashboard view model for MVC views.
/// Contains both admin and user dashboard data.
/// </summary>
public class DashboardViewModel
{
    #region Admin Statistics

    public int TotalUsers { get; set; }
    public int TotalListings { get; set; }
    public int ActiveListings { get; set; }
    public int TotalRentals { get; set; }
    public int ActiveRentals { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }

    #endregion

    #region User Statistics

    public int TotalListingsOwned { get; set; }
    public int ActiveListingsOwned { get; set; }
    public int TotalRentalsAsRenter { get; set; }
    public int TotalRentalsAsOwner { get; set; }
    public int ActiveRentalsAsRenter { get; set; }
    public int ActiveRentalsAsOwner { get; set; }

    #endregion

    #region Admin Data Lists

    /// <summary>All users (admin view).</summary>
    public List<ApplicationUser>? Users { get; set; }

    /// <summary>All listings (admin view).</summary>
    public List<Item>? Listings { get; set; }

    /// <summary>All rentals (admin view).</summary>
    public List<Rental>? Rentals { get; set; }

    /// <summary>Recent payments (admin view).</summary>
    public List<Payment>? RecentPayments { get; set; }

    #endregion

    #region User Data Lists

    /// <summary>Items this user owns.</summary>
    public List<Item>? ListingsOwned { get; set; }

    /// <summary>Rentals for items this user owns (owner's perspective).</summary>
    public List<Rental>? OwnerRentals { get; set; }

    /// <summary>Rentals where this user is the renter.</summary>
    public List<Rental>? MyRentals { get; set; }

    /// <summary>Items this user has favorited.</summary>
    public List<Item>? FavoriteItems { get; set; }

    #endregion
}
