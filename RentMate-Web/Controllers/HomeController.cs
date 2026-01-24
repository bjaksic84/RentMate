using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;

namespace RentMate.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly RentMateContext _context;

        public HomeController(ILogger<HomeController> logger, RentMateContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Get most popular items (highest rated with most reviews)
            var popularItems = await _context.Items
                .AsNoTracking()
                .Include(i => i.User)
                .Where(i => i.IsListed && !i.IsAdminHidden && i.AverageRating.HasValue)
                .OrderByDescending(i => i.AverageRating)
                .ThenByDescending(i => i.ReviewCount)
                .Take(8)
                .ToListAsync();

            // Get newest items
            var newItems = await _context.Items
                .AsNoTracking()
                .Include(i => i.User)
                .Where(i => i.IsListed && !i.IsAdminHidden)
                .OrderByDescending(i => i.CreatedAt)
                .Take(8)
                .ToListAsync();

            // Get all cities for the dropdown
            var cities = RentMate.Helpers.CityData.Cities.Select(c => c.Name).ToList();

            // Get category counts for stats
            var totalItems = await _context.Items.CountAsync(i => i.IsListed && !i.IsAdminHidden);
            var totalUsers = await _context.Users.CountAsync();
            var totalRentals = await _context.Rentals.CountAsync();

            ViewBag.PopularItems = popularItems;
            ViewBag.NewItems = newItems;
            ViewBag.Cities = cities;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalRentals = totalRentals;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
