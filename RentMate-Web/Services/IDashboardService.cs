using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services;

/// <summary>
/// Service for dashboard data retrieval.
/// Provides a single source of truth for both MVC and API controllers.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Get admin dashboard with platform-wide statistics.
    /// </summary>
    /// <param name="useCache">Whether to use cached statistics (default: true)</param>
    Task<AdminDashboardResponse> GetAdminDashboardAsync(bool useCache = true);
    
    /// <summary>
    /// Get user dashboard for a specific user.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    Task<UserDashboardResponse> GetUserDashboardAsync(string userId);
    
    /// <summary>
    /// Get combined dashboard for users who may also be admins.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="isAdmin">Whether the user is an admin</param>
    Task<CombinedDashboardResponse> GetCombinedDashboardAsync(string userId, bool isAdmin);
    
    /// <summary>
    /// Get rentals where the user is the renter.
    /// </summary>
    Task<List<RentalSummary>> GetMyRentalsAsync(string userId);
    
    /// <summary>
    /// Get rentals where the user is the owner (their items being rented).
    /// </summary>
    Task<List<RentalSummary>> GetOwnerRentalsAsync(string userId);
    
    /// <summary>
    /// Invalidate the admin dashboard cache.
    /// Call this after significant data changes.
    /// </summary>
    void InvalidateAdminCache();
}
