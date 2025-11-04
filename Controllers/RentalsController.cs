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

        // 🔹 Public listings: items available to rent
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var available = await _context.Items
                .Include(i => i.User)
                .Where(i => i.IsListed && !i.IsRented)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(available);
        }

        // 🔹 Step 1: Request a rental (Pending)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RequestRental(int itemId, DateTime startDate, DateTime endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var item = await _context.Items
                .Include(i => i.Rentals)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null || !item.IsListed)
                return NotFound("Item not available for rent.");

            if (item.UserId == user.Id)
                return BadRequest("You cannot rent your own item.");

            // Prevent overlapping active rentals
            bool hasConflict = item.Rentals!.Any(r =>
                (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending) &&
                r.StartDate <= endDate &&
                r.EndDate >= startDate);

            if (hasConflict)
                return BadRequest("Item is already booked during this period.");

            // Calculate total price
            int rentalDays = (endDate.Date - startDate.Date).Days;
            rentalDays = Math.Max(rentalDays, 1);
            decimal totalPrice = (item.Price ?? 0) * rentalDays;

            var rental = new Rental
            {
                ItemId = item.Id,
                OwnerId = item.UserId ?? string.Empty,
                RenterId = user.Id,
                StartDate = startDate,
                EndDate = endDate,
                Status = RentalStatus.Pending,
                TotalPrice = totalPrice
            };

            _context.Rentals.Add(rental);
            await _context.SaveChangesAsync();

            TempData["InfoMessage"] = "Rental request submitted. Awaiting owner approval.";
            return RedirectToAction("UserDashboard", "Dashboard");

        }

        // 🔹 Step 2: Owner approves rental
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ApproveRental(int rentalId)
        {
            var user = await _userManager.GetUserAsync(User);
            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null || rental.Item == null)
                return NotFound();

            if (rental.OwnerId != user.Id)
                return Forbid();

            rental.Status = RentalStatus.Active;
            rental.Item.IsRented = true;
            rental.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"You approved rental for {rental.Item.Title}.";
            return RedirectToAction(nameof(OwnerRentals));
        }

        // 🔹 Step 3a: Complete rental
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CompleteRental(int rentalId)
        {
            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (rental.OwnerId != user.Id && rental.RenterId != user.Id)
                return Forbid();

            rental.Status = RentalStatus.Completed;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;
            rental.EndDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Rental completed successfully.";
            return RedirectToAction(nameof(MyRentals));
        }

        // 🔹 Step 3b: Cancel rental (either party)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CancelRental(int rentalId)
        {
            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (rental.OwnerId != user.Id && rental.RenterId != user.Id)
                return Forbid();

            rental.Status = RentalStatus.Cancelled;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["InfoMessage"] = "Rental request was cancelled.";
            return RedirectToAction(nameof(MyRentals));
        }

        // 🔹 My rentals (as renter)
        public async Task<IActionResult> MyRentals()
        {
            var user = await _userManager.GetUserAsync(User);
            var rentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.RenterId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(rentals);
        }

        // 🔹 Rentals of my items (as owner)
        public async Task<IActionResult> OwnerRentals()
        {
            var user = await _userManager.GetUserAsync(User);
            var rentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.OwnerId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(rentals);
        }
    }
}


