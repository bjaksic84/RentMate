using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using System.Linq;
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
                return RedirectToAction(nameof(AdminDashboard));

            return RedirectToAction(nameof(UserDashboard));
        }

        // --- ADMIN DASHBOARD ---
        [Authorize(Roles = "Admin")]
        public IActionResult AdminDashboard()
        {
            var viewModel = new DashboardViewModel
            {
                TotalUsers = _userManager.Users.Count(),
                TotalListings = _context.Items.Count(),
                ActiveListings = _context.Items.Count(i => i.IsListed),
                TotalRentals = _context.Rentals.Count(),
                ActiveRentals = _context.Rentals.Count(r => r.Status == RentalStatus.Active),

                Users = _userManager.Users.ToList(),
                Listings = _context.Items
                    .Include(i => i.User)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToList(),
                Rentals = _context.Rentals
                    .Include(r => r.Item)
                    .Include(r => r.Renter)
                    .Include(r => r.Owner)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList()
            };

            return View(viewModel);
        }

        // --- USER DASHBOARD ---
        [Authorize]
        public async Task<IActionResult> UserDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Index", "Home");

            // 1️⃣ Items owned by the user
            var userItems = await _context.Items
                .Where(i => i.UserId == user.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            // 2️⃣ Rentals where the user is the renter
            var myRentals = await _context.Rentals
                .Include(r => r.Item)
                .Include(r => r.Owner)
                .Where(r => r.RenterId == user.Id)
                .OrderByDescending(r => r.StartDate)
                .ToListAsync();

            // 3️⃣ Rentals where the user is the owner
            var ownerRentals = await _context.Rentals
                .Include(r => r.Item)
                .Include(r => r.Renter)
                .Where(r => r.OwnerId == user.Id)
                .OrderByDescending(r => r.StartDate)
                .ToListAsync();

            // Build model
            var viewModel = new DashboardViewModel
            {
                Listings = userItems,
                MyRentals = myRentals,
                OwnerRentals = ownerRentals,
                TotalListings = userItems.Count,
                ActiveListings = userItems.Count(i => i.IsListed && !i.IsRented),
                TotalRentals = myRentals.Count + ownerRentals.Count,
                ActiveRentals = myRentals.Count(r => r.Status == RentalStatus.Active)
            };

            return View(viewModel);
        }
    }
}


