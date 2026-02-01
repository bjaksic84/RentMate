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
        #region Constants

        /// <summary>Maximum number of items to display in dashboard lists.</summary>
        private const int DashboardListLimit = 10;
        private const string AdminRole = "Admin";
        private const string DashboardErrorMessage = "An error occurred while loading the dashboard.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RentMateContext _context;
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        #endregion

        #region Constructor

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

        #endregion

        #region Public Actions

        /// <summary>
        /// Redirects to the appropriate dashboard based on user role.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            
            if (user != null && await _userManager.IsInRoleAsync(user, AdminRole))
            {
                return RedirectToAction(nameof(AdminDashboard));
            }

            return RedirectToAction(nameof(UserDashboard));
        }

        /// <summary>
        /// Displays the admin dashboard with platform-wide statistics.
        /// </summary>
        [Authorize(Roles = AdminRole)]
        public async Task<IActionResult> AdminDashboard()
        {
            try
            {
                var response = await _dashboardService.GetAdminDashboardAsync();
                var viewModel = response.ToLegacyViewModel();
                
                await PopulateAdminDashboardEntitiesAsync(viewModel);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                return HandleDashboardError();
            }
        }

        /// <summary>
        /// Displays the user dashboard with personal statistics and data.
        /// </summary>
        [Authorize]
        public async Task<IActionResult> UserDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var response = await _dashboardService.GetUserDashboardAsync(user.Id);
                var viewModel = response.ToLegacyViewModel();
                
                await PopulateUserDashboardEntitiesAsync(viewModel, user.Id);
                SetUserDashboardDebugInfo(user.Id, viewModel.ListingsOwned?.Count ?? 0);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user dashboard for {UserId}", user.Id);
                return HandleDashboardError();
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Populates entity lists needed by the admin dashboard view.
        /// </summary>
        private async Task PopulateAdminDashboardEntitiesAsync(DashboardViewModel viewModel)
        {
            viewModel.Users = await _userManager.Users
                .AsNoTracking()
                .Take(DashboardListLimit)
                .ToListAsync();

            viewModel.Listings = await BuildRecentItemsQuery()
                .Take(DashboardListLimit)
                .ToListAsync();

            viewModel.Rentals = await BuildRecentRentalsQuery(includeAllParties: true)
                .Take(DashboardListLimit)
                .ToListAsync();
        }

        /// <summary>
        /// Populates entity lists needed by the user dashboard view.
        /// </summary>
        private async Task PopulateUserDashboardEntitiesAsync(DashboardViewModel viewModel, string userId)
        {
            viewModel.ListingsOwned = await _context.Items
                .AsNoTracking()
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            viewModel.MyRentals = await BuildUserRentalsQuery(userId, asRenter: true)
                .ToListAsync();

            viewModel.OwnerRentals = await BuildUserRentalsQuery(userId, asRenter: false)
                .ToListAsync();

            viewModel.FavoriteItems = await BuildUserFavoritesQuery(userId)
                .ToListAsync();
        }

        /// <summary>
        /// Builds query for recent items with owner information.
        /// </summary>
        private IQueryable<Item> BuildRecentItemsQuery()
        {
            return _context.Items
                .AsNoTracking()
                .Include(i => i.User)
                .OrderByDescending(i => i.CreatedAt);
        }

        /// <summary>
        /// Builds query for recent rentals with related entities.
        /// </summary>
        private IQueryable<Rental> BuildRecentRentalsQuery(bool includeAllParties)
        {
            IQueryable<Rental> query = _context.Rentals
                .AsNoTracking()
                .Include(r => r.Item);

            if (includeAllParties)
            {
                query = query
                    .Include(r => r.Renter)
                    .Include(r => r.Owner);
            }

            return query.OrderByDescending(r => r.CreatedAt);
        }

        /// <summary>
        /// Builds query for user's rentals (either as renter or owner).
        /// </summary>
        private IQueryable<Rental> BuildUserRentalsQuery(string userId, bool asRenter)
        {
            IQueryable<Rental> query = _context.Rentals
                .AsNoTracking()
                .Include(r => r.Item);

            if (asRenter)
            {
                query = query
                    .Include(r => r.Owner)
                    .Where(r => r.RenterId == userId);
            }
            else
            {
                query = query
                    .Include(r => r.Renter)
                    .Where(r => r.OwnerId == userId);
            }

            return query.OrderByDescending(r => r.StartDate);
        }

        /// <summary>
        /// Builds query for user's favorite items.
        /// </summary>
        private IQueryable<Item> BuildUserFavoritesQuery(string userId)
        {
            return _context.AccountItemFavorites
                .AsNoTracking()
                .Where(f => f.AccountId == userId)
                .Include(f => f.Item)
                    .ThenInclude(i => i.User)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Item);
        }

        /// <summary>
        /// Handles dashboard loading errors by redirecting to home with error message.
        /// </summary>
        private IActionResult HandleDashboardError()
        {
            TempData["ErrorMessage"] = DashboardErrorMessage;
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Sets debug information for the user dashboard view.
        /// </summary>
        private void SetUserDashboardDebugInfo(string userId, int itemCount)
        {
            ViewData["DebugUserId"] = userId;
            ViewData["FoundItems"] = itemCount;
        }

        #endregion
    }
}