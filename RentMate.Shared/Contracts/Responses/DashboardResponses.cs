namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Admin dashboard response - contains platform-wide statistics and recent activity.
/// Used by both Web (AdminDashboard.cshtml) and Mobile admin views.
/// </summary>
public record AdminDashboardResponse
{
    // Platform statistics
    public int TotalUsers { get; init; }
    public int TotalListings { get; init; }
    public int ActiveListings { get; init; }
    public int TotalRentals { get; init; }
    public int ActiveRentals { get; init; }
    
    // Financial metrics
    public decimal TotalRevenue { get; init; }
    public decimal MonthlyRevenue { get; init; }
    
    // Recent activity (for admin review)
    public List<UserWithRoles> RecentUsers { get; init; } = new();
    public List<ItemSummary> RecentListings { get; init; } = new();
    public List<RentalSummary> RecentRentals { get; init; } = new();
    public List<PaymentSummary> RecentPayments { get; init; } = new();
}

/// <summary>
/// User dashboard response - contains user-specific statistics and their activity.
/// Used by both Web (UserDashboard.cshtml) and Mobile Dashboard.razor.
/// </summary>
public record UserDashboardResponse
{
    // User's ownership statistics
    public int TotalListingsOwned { get; init; }
    public int ActiveListingsOwned { get; init; }
    
    // User's rental statistics (as renter)
    public int TotalRentalsAsRenter { get; init; }
    public int ActiveRentalsAsRenter { get; init; }
    
    // User's rental statistics (as owner - people renting their items)
    public int TotalRentalsAsOwner { get; init; }
    public int ActiveRentalsAsOwner { get; init; }
    
    // User's items
    public List<ItemSummary> MyListings { get; init; } = new();
    
    // Rentals where user is the renter (items they're renting FROM others)
    public List<RentalSummary> MyRentals { get; init; } = new();
    
    // Rentals where user is the owner (items they're renting TO others)
    public List<RentalSummary> OwnerRentals { get; init; } = new();
    
    // Recent payments involving this user
    public List<PaymentSummary> RecentPayments { get; init; } = new();
}

/// <summary>
/// Combined dashboard for users who are also admins - contains both views.
/// </summary>
public record CombinedDashboardResponse
{
    public AdminDashboardResponse? AdminData { get; init; }
    public UserDashboardResponse UserData { get; init; } = new();
    public bool IsAdmin { get; init; }
}
