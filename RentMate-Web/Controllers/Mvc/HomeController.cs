using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;

namespace RentMate.Controllers.Mvc
{
    /// <summary>
    /// Controller for public-facing home pages and error handling.
    /// </summary>
    public class HomeController : Controller
    {
        #region Constants

        /// <summary>Number of items to display in each homepage section.</summary>
        private const int FeaturedItemsCount = 8;

        #endregion

        #region Dependencies

        private readonly ILogger<HomeController> _logger;
        private readonly RentMateContext _context;

        #endregion

        #region Constructor

        public HomeController(ILogger<HomeController> logger, RentMateContext context)
        {
            _logger = logger;
            _context = context;
        }

        #endregion

        #region Public Actions

        /// <summary>
        /// Displays the homepage with featured items and platform statistics.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var popularItems = await GetPopularItemsAsync();
            var newestItems = await GetNewestItemsAsync();
            var platformStats = await GetPlatformStatisticsAsync();

            SetHomePageViewData(popularItems, newestItems, platformStats);

            return View();
        }

        /// <summary>
        /// Displays the privacy policy page.
        /// </summary>
        public IActionResult Privacy() => View();

        /// <summary>
        /// Displays a generic error page with request tracking information.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            return View(new ErrorViewModel { RequestId = requestId });
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Retrieves the most popular items based on rating and review count.
        /// </summary>
        private Task<List<Item>> GetPopularItemsAsync()
        {
            return BuildVisibleItemsQuery()
                .Where(i => i.AverageRating.HasValue)
                .OrderByDescending(i => i.AverageRating)
                .ThenByDescending(i => i.ReviewCount)
                .Take(FeaturedItemsCount)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves the newest listed items.
        /// </summary>
        private Task<List<Item>> GetNewestItemsAsync()
        {
            return BuildVisibleItemsQuery()
                .OrderByDescending(i => i.CreatedAt)
                .Take(FeaturedItemsCount)
                .ToListAsync();
        }

        /// <summary>
        /// Builds the base query for publicly visible items.
        /// </summary>
        private IQueryable<Item> BuildVisibleItemsQuery()
        {
            return _context.Items
                .AsNoTracking()
                .Include(i => i.User)
                .Where(i => i.IsListed && !i.IsAdminHidden);
        }

        /// <summary>
        /// Retrieves platform-wide statistics for display.
        /// </summary>
        private async Task<(int TotalItems, int TotalUsers, int TotalRentals)> GetPlatformStatisticsAsync()
        {
            var totalItems = await _context.Items.CountAsync(i => i.IsListed && !i.IsAdminHidden);
            var totalUsers = await _context.Users.CountAsync();
            var totalRentals = await _context.Rentals.CountAsync();

            return (totalItems, totalUsers, totalRentals);
        }

        /// <summary>
        /// Sets all ViewBag data needed for the homepage view.
        /// </summary>
        private void SetHomePageViewData(
            List<Item> popularItems, 
            List<Item> newestItems, 
            (int TotalItems, int TotalUsers, int TotalRentals) stats)
        {
            var availableCities = RentMate.Helpers.CityData.Cities.Select(c => c.Name).ToList();

            ViewBag.PopularItems = popularItems;
            ViewBag.NewItems = newestItems;
            ViewBag.Cities = availableCities;
            ViewBag.TotalItems = stats.TotalItems;
            ViewBag.TotalUsers = stats.TotalUsers;
            ViewBag.TotalRentals = stats.TotalRentals;
        }

        #endregion
    }
}

