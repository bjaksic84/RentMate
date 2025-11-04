using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;

namespace RentMate.Controllers
{
    [Authorize]
    public class RentalsController : Controller
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RentalsController(RentMateContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1️⃣  Public listings — all items available to rent
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var available = await _context.Items
                .Where(i => i.IsListed && !i.IsRented)
                .ToListAsync();
            return View(available);
        }

        // 2️⃣  Start a rental
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RentItem(int itemId, DateTime startDate, DateTime endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var item = await _context.Items.FindAsync(itemId);
            if (item == null || !item.IsListed || item.IsRented)
                return NotFound("Item not available for rent.");

            item.IsRented = true;

            var rental = new Rental
            {
                ItemId = item.Id,
                RenterId = user.Id,
                StartDate = startDate,
                EndDate = endDate,
                Status = RentalStatus.Active
            };

            _context.Rentals.Add(rental);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Successfully rented {item.Title}!";

            return RedirectToAction("UserDashboard", "Dashboard");
        }


        // 3️⃣  End (complete) a rental
        [HttpPost]
        public async Task<IActionResult> CompleteRental(int rentalId)
        {
            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null) return NotFound();

            rental.Status = RentalStatus.Completed;
            rental.EndDate = DateTime.UtcNow;
            rental.Item!.IsRented = false;

            await _context.SaveChangesAsync();
            return RedirectToAction("MyRentals");
        }

        // 4️⃣  Show the logged-in user’s rentals
        public async Task<IActionResult> MyRentals()
        {
            var user = await _userManager.GetUserAsync(User);
            var rentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.RenterId == user.Id)
                .ToListAsync();
            return View(rentals);
        }

        // 5️⃣  Toggle listing visibility (list / unlist)
        [HttpPost]
        public async Task<IActionResult> ToggleListing(int itemId)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.Items.FindAsync(itemId);

            if (item == null || item.UserId != user.Id)
                return Unauthorized();

            item.IsListed = !item.IsListed;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Dashboard");
        }
    }
}

