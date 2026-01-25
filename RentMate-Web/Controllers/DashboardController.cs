using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using RentMate.Services;
using RentMate.Shared.Contracts.Responses;
using Microsoft.Extensions.Caching.Memory;

namespace RentMate.Controllers
{
    /// <summary>
    /// MVC Controller for dashboard views.
    /// Uses IDashboardService for business logic, with legacy mapping for views.
    /// </summary>
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RentMateContext _context;
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            UserManager<ApplicationUser> userManager, 
            RentMateContext context,
            IDashboardService dashboardService,
            ILogger<DashboardController> logger)
        {
            _userManager = userManager;
            _context = context;
            _dashboardService = dashboardService;
            _logger = logger;
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
            try
            {
                // Get statistics from service (uses caching internally)
                var response = await _dashboardService.GetAdminDashboardAsync();
                
                // Convert to legacy ViewModel for the view
                var viewModel = response.ToLegacyViewModel();
                
                // Populate the entity lists needed by the view
                // (The view uses navigation properties that DTOs don't have)
                viewModel.Users = await _userManager.Users.AsNoTracking().Take(10).ToListAsync();
                viewModel.Listings = await _context.Items.AsNoTracking()
                    .Include(i => i.User)
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(10).ToListAsync();
                viewModel.Rentals = await _context.Rentals.AsNoTracking()
                    .Include(r => r.Item)
                    .Include(r => r.Renter)
                    .Include(r => r.Owner)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(10).ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return RedirectToAction("Index", "Home");
            }
        }

        // --- USER DASHBOARD ---
        [Authorize]
        public async Task<IActionResult> UserDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Index", "Home");

            try
            {
                // Get statistics from service
                var response = await _dashboardService.GetUserDashboardAsync(user.Id);
                
                // Convert to legacy ViewModel for the view
                var viewModel = response.ToLegacyViewModel();
                
                // Populate the entity lists needed by the view
                // (The view uses navigation properties that DTOs don't have)
                viewModel.ListingsOwned = await _context.Items
                    .AsNoTracking()
                    .Where(i => i.UserId == user.Id)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();

                viewModel.MyRentals = await _context.Rentals
                    .AsNoTracking()
                    .Include(r => r.Item)
                    .Include(r => r.Owner)
                    .Where(r => r.RenterId == user.Id)
                    .OrderByDescending(r => r.StartDate)
                    .ToListAsync();

                viewModel.OwnerRentals = await _context.Rentals
                    .AsNoTracking()
                    .Include(r => r.Item)
                    .Include(r => r.Renter)
                    .Where(r => r.OwnerId == user.Id)
                    .OrderByDescending(r => r.StartDate)
                    .ToListAsync();

                ViewData["DebugUserId"] = user.Id;
                ViewData["FoundItems"] = viewModel.ListingsOwned?.Count ?? 0;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user dashboard for {UserId}", user.Id);
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}