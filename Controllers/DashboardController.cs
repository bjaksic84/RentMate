using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using System.Threading.Tasks;

namespace RentMate.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RentMateContext _context;

        public DashboardController(UserManager<ApplicationUser> userManager, RentMateContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("AdminDashboard");

            return RedirectToAction("UserDashboard");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult AdminDashboard()
        {
            var viewModel = new DashboardViewModel
            {
                TotalUsers = _userManager.Users.Count(),
                TotalListings = _context.Items.Count(),
                ActiveListings = _context.Items.Count(i => i.IsListed), // or whatever property marks availability
                TotalRentals = _context.Rentals.Count(),

                // optional lists for later display
                Users = _userManager.Users.ToList(),
                Listings = _context.Items.ToList(),
                Rentals = _context.Rentals.ToList()
            };

            return View(viewModel);
        }


        [Authorize(Roles = "User,Admin")]
        public IActionResult UserDashboard()
        {
            return View();
        }
    }
}


