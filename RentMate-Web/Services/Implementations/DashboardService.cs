using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.Dto;
using RentMate.Shared.Contracts.Responses;

// Use alias for RentalStatus to distinguish from Model's which doesn't exist anymore
using RentalStatus = RentMate.Shared.Contracts.Responses.RentalStatus;

using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations;

/// <summary>
/// Dashboard service implementation.
/// Single source of truth for all dashboard data - used by both MVC and API controllers.
/// </summary>
public class DashboardService : IDashboardService
{
    private const string AdminStatsCacheKey = "AdminDashboardStats";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    
    private readonly RentMateContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        RentMateContext context,
        UserManager<ApplicationUser> userManager,
        IMemoryCache cache,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _userManager = userManager;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AdminDashboardResponse> GetAdminDashboardAsync(bool useCache = true)
    {
        // Try to get cached statistics
        if (useCache && _cache.TryGetValue(AdminStatsCacheKey, out AdminDashboardResponse? cachedStats) && cachedStats != null)
        {
            _logger.LogDebug("Returning cached admin dashboard stats");
            
            // Return cached stats but with fresh recent activity lists
            return cachedStats with
            {
                RecentUsers = await GetRecentUsersAsync(10),
                RecentListings = await GetRecentListingsAsync(10),
                RecentRentals = await GetRecentRentalsAsync(10),
                RecentPayments = await GetRecentPaymentsAsync(10)
            };
        }

        _logger.LogDebug("Building fresh admin dashboard stats");

        // Calculate statistics with efficient single queries
        var stats = new AdminDashboardResponse
        {
            TotalUsers = await _userManager.Users.CountAsync(),
            TotalListings = await _context.Items.CountAsync(),
            ActiveListings = await _context.Items.CountAsync(i => i.IsListed && !i.IsAdminHidden),
            TotalRentals = await _context.Rentals.CountAsync(),
            ActiveRentals = await _context.Rentals.CountAsync(r => r.Status == RentalStatus.Active),
            TotalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Success)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m,
            MonthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Success && 
                           p.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .SumAsync(p => (decimal?)p.Amount) ?? 0m,
            
            // Fresh recent activity
            RecentUsers = await GetRecentUsersAsync(10),
            RecentListings = await GetRecentListingsAsync(10),
            RecentRentals = await GetRecentRentalsAsync(10),
            RecentPayments = await GetRecentPaymentsAsync(10)
        };

        // Cache the statistics (but not the lists - they change too often)
        var statsForCache = stats with
        {
            RecentUsers = new List<UserWithRoles>(),
            RecentListings = new List<ItemSummary>(),
            RecentRentals = new List<RentalSummary>(),
            RecentPayments = new List<PaymentSummary>()
        };
        
        _cache.Set(AdminStatsCacheKey, statsForCache, CacheDuration);

        return stats;
    }

    public async Task<UserDashboardResponse> GetUserDashboardAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        _logger.LogDebug("Building user dashboard for {UserId}", userId);

        // Get user's listings with projection
        var myListings = await _context.Items
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ItemSummary(
                i.Id,
                i.Title ?? "Untitled",
                i.Description,
                i.Price ?? 0m,
                i.ImageUrl,
                i.Location,
                i.Category,
                i.IsListed,
                i.IsAdminHidden,
                i.AverageRating ?? 0.0,
                i.ReviewCount,
                i.CreatedAt
            ))
            .ToListAsync();

        // Get rentals where user is renter
        var myRentals = await GetRentalSummariesAsync(r => r.RenterId == userId);

        // Get rentals where user is owner
        var ownerRentals = await GetRentalSummariesAsync(r => r.OwnerId == userId);

        // Get user's recent payments
        var recentPayments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Rental)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new PaymentSummary(
                p.Id,
                p.Amount,
                p.CreatedAt,
                "Card", // Default payment method since not stored
                p.TransactionId,
                p.RentalId,
                p.User != null ? p.User.UserName ?? "Unknown" : "Unknown"
            ))
            .ToListAsync();

        return new UserDashboardResponse
        {
            TotalListingsOwned = myListings.Count,
            ActiveListingsOwned = myListings.Count(i => i.IsListed && !i.IsAdminHidden),
            TotalRentalsAsRenter = myRentals.Count,
            ActiveRentalsAsRenter = myRentals.Count(r => r.Status == Shared.Contracts.Responses.RentalStatus.Active),
            TotalRentalsAsOwner = ownerRentals.Count,
            ActiveRentalsAsOwner = ownerRentals.Count(r => r.Status == Shared.Contracts.Responses.RentalStatus.Active),
            MyListings = myListings,
            MyRentals = myRentals,
            OwnerRentals = ownerRentals,
            RecentPayments = recentPayments
        };
    }

    public async Task<CombinedDashboardResponse> GetCombinedDashboardAsync(string userId, bool isAdmin)
    {
        var userDashboard = await GetUserDashboardAsync(userId);
        
        return new CombinedDashboardResponse
        {
            UserData = userDashboard,
            AdminData = isAdmin ? await GetAdminDashboardAsync() : null,
            IsAdmin = isAdmin
        };
    }

    public async Task<List<RentalSummary>> GetMyRentalsAsync(string userId)
    {
        return await GetRentalSummariesAsync(r => r.RenterId == userId);
    }

    public async Task<List<RentalSummary>> GetOwnerRentalsAsync(string userId)
    {
        return await GetRentalSummariesAsync(r => r.OwnerId == userId);
    }

    public void InvalidateAdminCache()
    {
        _cache.Remove(AdminStatsCacheKey);
        _logger.LogInformation("Admin dashboard cache invalidated");
    }

    // ============ Private Helper Methods ============

    private async Task<List<UserWithRoles>> GetRecentUsersAsync(int count)
    {
        // Efficient single query with join for roles
        var usersWithRoles = await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.Id) // Assuming newer users have higher IDs or use a CreatedAt field
            .Take(count)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FirstName,
                u.LastName,
                u.City,
                u.ProfilePictureUrl,
                Roles = _context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .ToList()
            })
            .ToListAsync();

        return usersWithRoles.Select(u => new UserWithRoles(
            u.Id,
            u.UserName ?? "Unknown",
            u.Email,
            u.FirstName,
            u.LastName,
            u.City,
            u.ProfilePictureUrl,
            u.Roles.Where(r => r != null).Select(r => r!).ToList()
        )).ToList();
    }

    private async Task<List<ItemSummary>> GetRecentListingsAsync(int count)
    {
        return await _context.Items
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .Select(i => new ItemSummary(
                i.Id,
                i.Title ?? "Untitled",
                i.Description,
                i.Price ?? 0m,
                i.ImageUrl,
                i.Location,
                i.Category,
                i.IsListed,
                i.IsAdminHidden,
                i.AverageRating ?? 0.0,
                i.ReviewCount,
                i.CreatedAt
            ))
            .ToListAsync();
    }

    private async Task<List<RentalSummary>> GetRecentRentalsAsync(int count)
    {
        return await _context.Rentals
            .AsNoTracking()
            .Include(r => r.Item)
            .Include(r => r.Renter)
            .Include(r => r.Owner)
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .Select(r => new RentalSummary(
                r.Id,
                r.Item != null ? r.Item.Title ?? "Unknown Item" : "Unknown Item",
                r.Item != null ? r.Item.Id : 0,
                r.Item != null ? r.Item.ImageUrl : null,
                r.Renter != null ? r.Renter.UserName ?? "Unknown" : "Unknown",
                r.Owner != null ? r.Owner.UserName ?? "Unknown" : "Unknown",
                r.StartDate,
                r.EndDate,
                r.TotalPrice,
                r.Status,
                r.CreatedAt
            ))
            .ToListAsync();
    }

    private async Task<List<PaymentSummary>> GetRecentPaymentsAsync(int count)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .Select(p => new PaymentSummary(
                p.Id,
                p.Amount,
                p.CreatedAt,
                "Card", // Default payment method since not stored
                p.TransactionId,
                p.RentalId,
                p.User != null ? p.User.UserName ?? "Unknown" : "Unknown"
            ))
            .ToListAsync();
    }

    private async Task<List<RentalSummary>> GetRentalSummariesAsync(
        System.Linq.Expressions.Expression<Func<Rental, bool>> predicate)
    {
        return await _context.Rentals
            .AsNoTracking()
            .Include(r => r.Item)
            .Include(r => r.Renter)
            .Include(r => r.Owner)
            .Where(predicate)
            .OrderByDescending(r => r.StartDate)
            .Select(r => new RentalSummary(
                r.Id,
                r.Item != null ? r.Item.Title ?? "Unknown Item" : "Unknown Item",
                r.Item != null ? r.Item.Id : 0,
                r.Item != null ? r.Item.ImageUrl : null,
                r.Renter != null ? r.Renter.UserName ?? "Unknown" : "Unknown",
                r.Owner != null ? r.Owner.UserName ?? "Unknown" : "Unknown",
                r.StartDate,
                r.EndDate,
                r.TotalPrice,
                r.Status,
                r.CreatedAt
            ))
            .ToListAsync();
    }
}

