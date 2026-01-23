using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using RentMate.Shared;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;

namespace RentMate.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RentMateContext _context;
        private readonly IMemoryCache _cache;

        public DashboardController(UserManager<ApplicationUser> userManager, RentMateContext context, IMemoryCache cache)
        {
            _userManager = userManager;
            _context = context;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction(nameof(AdminDashboard));

            return RedirectToAction(nameof(UserDashboard));
        }

        // --- ADMIN DASHBOARD ---
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            const string cacheKey = "AdminStats";
            if (!_cache.TryGetValue(cacheKey, out RentMate.Models.DashboardViewModel? stats))
            {
                stats = new RentMate.Models.DashboardViewModel
                {
                    TotalUsers = _userManager.Users.Count(),
                    TotalListings = _context.Items.Count(),
                    ActiveListings = _context.Items.Count(i => i.IsListed),
                    TotalRentals = _context.Rentals.Count(),
                    ActiveRentals = _context.Rentals.Count(r => r.Status == RentalStatus.Active),
                    TotalRevenue = await _context.Payments
                        .Where(p => p.Status == PaymentStatus.Success)
                        .SumAsync(p => p.Amount)
                };

                _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(15));
            }

            // Fresh data for the recent lists (don't cache these as they change often)
            stats!.Users = await _userManager.Users.AsNoTracking().Take(10).ToListAsync();
            stats.Listings = await _context.Items.AsNoTracking()
                .Include(i => i.User)
                .OrderByDescending(i => i.CreatedAt)
                .Take(10).ToListAsync();
            
            stats.Rentals = await _context.Rentals.AsNoTracking()
                .Include(r => r.Item)
                .Include(r => r.Renter)
                .Include(r => r.Owner)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10).ToListAsync();

            return View(stats);
        }

        // --- USER DASHBOARD ---
        
        [Authorize]
        public async Task<IActionResult> UserDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Index", "Home");

            // 1. Items owned by the user
            var userItems = await _context.Items
                .AsNoTracking()
                .Where(i => i.UserId == user.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            // 2. Rentals where the user is the renter
            var myRentals = await _context.Rentals
                .AsNoTracking()
                .Include(r => r.Item)
                .Include(r => r.Owner)
                .Where(r => r.RenterId == user.Id)
                .OrderByDescending(r => r.StartDate)
                .ToListAsync();

            // 3. Rentals where the user is the owner
            var ownerRentals = await _context.Rentals
                .AsNoTracking()
                .Include(r => r.Item)
                .Include(r => r.Renter)
                .Where(r => r.OwnerId == user.Id)
                .OrderByDescending(r => r.StartDate)
                .ToListAsync();

            // Build view model
            var viewModel = new RentMate.Models.DashboardViewModel
            {
                ListingsOwned = userItems,
                MyRentals = myRentals,
                OwnerRentals = ownerRentals,

                // Counts
                TotalListingsOwned = userItems.Count,
                ActiveListingsOwned = userItems.Count(i => i.IsListed && !i.IsRented),

                TotalRentalsAsRenter = myRentals.Count,
                TotalRentalsAsOwner = ownerRentals.Count,

                // (Optional global summaries for display)
                TotalListings = userItems.Count,
                ActiveListings = userItems.Count(i => i.IsListed),
                TotalRentals = myRentals.Count + ownerRentals.Count,
                ActiveRentals = myRentals.Count(r => r.Status == RentalStatus.Active)
                    + ownerRentals.Count(r => r.Status == RentalStatus.Active)
            };

            // Expose simple debug info to the view so it's visible in the UI if console logging isn't available
            ViewData["DebugUserId"] = user.Id;
            ViewData["FoundItems"] = userItems.Count;

            return View(viewModel);
        }
    }
}