using RentMate.Models;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services;

/// <summary>
/// Extension methods for mapping between new response DTOs and legacy ViewModels.
/// Used during transition period while views are migrated to new DTOs.
/// </summary>
public static class DashboardMappingExtensions
{
    /// <summary>
    /// Convert AdminDashboardResponse to the legacy DashboardViewModel for MVC views.
    /// </summary>
    public static DashboardViewModel ToLegacyViewModel(
        this AdminDashboardResponse response,
        List<ApplicationUser>? users = null,
        List<Item>? listings = null,
        List<Rental>? rentals = null)
    {
        return new DashboardViewModel
        {
            TotalUsers = response.TotalUsers,
            TotalListings = response.TotalListings,
            ActiveListings = response.ActiveListings,
            TotalRentals = response.TotalRentals,
            ActiveRentals = response.ActiveRentals,
            TotalRevenue = response.TotalRevenue,
            MonthlyRevenue = response.MonthlyRevenue,
            // These need to be populated separately with full EF entities for legacy views
            Users = users,
            Listings = listings,
            Rentals = rentals
        };
    }

    /// <summary>
    /// Convert UserDashboardResponse to the legacy DashboardViewModel for MVC views.
    /// </summary>
    public static DashboardViewModel ToLegacyViewModel(
        this UserDashboardResponse response,
        List<Item>? listingsOwned = null,
        List<Rental>? myRentals = null,
        List<Rental>? ownerRentals = null)
    {
        return new DashboardViewModel
        {
            TotalListingsOwned = response.TotalListingsOwned,
            ActiveListingsOwned = response.ActiveListingsOwned,
            TotalRentalsAsRenter = response.TotalRentalsAsRenter,
            TotalRentalsAsOwner = response.TotalRentalsAsOwner,
            TotalListings = response.TotalListingsOwned,
            ActiveListings = response.ActiveListingsOwned,
            TotalRentals = response.TotalRentalsAsRenter + response.TotalRentalsAsOwner,
            ActiveRentals = response.ActiveRentalsAsRenter + response.ActiveRentalsAsOwner,
            // These need to be populated separately with full EF entities for legacy views
            ListingsOwned = listingsOwned,
            MyRentals = myRentals,
            OwnerRentals = ownerRentals
        };
    }
}
