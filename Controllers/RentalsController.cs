using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using Microsoft.AspNetCore.SignalR;
using RentMate.Hubs;

namespace RentMate.Controllers
{
    [Authorize]
    public class RentalsController : Controller
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly IHubContext<RentMateHub> _hubContext;

        public RentalsController(RentMateContext context, UserManager<ApplicationUser> userManager, IHubContext<RentMateHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRental(int itemId, DateTime startDate, DateTime endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var item = await _context.Items
                .Include(i => i.Rentals)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null || !item.IsListed)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest("Item not available for rent.");

                TempData["ErrorMessage"] = "Item not available for rent.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (item.UserId == user.Id)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest("You cannot rent your own item.");

                TempData["ErrorMessage"] = "You cannot rent your own item.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            // Prevent overlapping rentals
            bool hasConflict = item.Rentals!.Any(r =>
                (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending) &&
                r.StartDate <= endDate &&
                r.EndDate >= startDate);

            if (hasConflict)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest("Item is already booked during this period.");

                TempData["ErrorMessage"] = "Item is already booked during this period.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            // Calculate total price
            int rentalDays = Math.Max((endDate.Date - startDate.Date).Days, 1);
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

            // ✅ Send real-time notification to the owner
            await _hubContext.Clients.User(item.UserId!).SendAsync("RentalRequested", new
            {
                rentalId = rental.Id,
                itemTitle = item.Title,
                renterEmail = user.Email,
                startDate = rental.StartDate.ToShortDateString(),
                endDate = rental.EndDate.ToShortDateString(),
                status = rental.Status.ToString()
            });

            // ✅ If AJAX call — return JSON success
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = "Rental request submitted successfully." });

            // ✅ Otherwise — normal redirect (for dashboard form)
            TempData["SuccessMessage"] = "Rental request submitted. Awaiting owner approval.";
            return RedirectToAction("UserDashboard", "Dashboard");
        }


        // 🔹 Step 2: Owner approves rental
        [HttpPost]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ApproveRental(int rentalId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null || rental.Item == null)
            {
                TempData["ErrorMessage"] = "Rental not found.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.OwnerId != user.Id)
            {
                TempData["ErrorMessage"] = "You are not authorized to approve this rental.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            rental.Status = RentalStatus.Active;
            rental.Item.IsRented = true;
            rental.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();


            // Notify renter that their request was approved
            await _hubContext.Clients.User(rental.RenterId!).SendAsync("RentalStatusChanged", new
            {
                rentalId = rental.Id,
                newStatus = rental.Status.ToString(),
                itemTitle = rental.Item.Title,
                message = $"Your rental request for '{rental.Item.Title}' was approved!"
            });

            TempData["SuccessMessage"] = $"You approved rental for '{rental.Item.Title}'.";
            return RedirectToAction("UserDashboard", "Dashboard");
        }


        // 🔹 Step 3a: Complete rental
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteRental(int rentalId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null)
            {
                TempData["ErrorMessage"] = "Rental not found.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.OwnerId != user.Id && rental.RenterId != user.Id)
            {
                TempData["ErrorMessage"] = "You are not authorized to complete this rental.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            rental.Status = RentalStatus.Completed;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;
            rental.EndDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            // Notify renter that rental was completed
            await _hubContext.Clients.User(rental.RenterId!).SendAsync("RentalStatusChanged", new
            {
                rentalId = rental.Id,
                newStatus = rental.Status.ToString(),
                itemTitle = rental.Item.Title,
                message = $"Rental for '{rental.Item.Title}' was marked as completed."
            });
            TempData["SuccessMessage"] = $"Rental for '{rental.Item.Title}' completed successfully.";
            return RedirectToAction("UserDashboard", "Dashboard");
        }


        // 🔹 Step 3b: Cancel rental (either party)
        [HttpPost]
        [Authorize]
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRental(int rentalId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null)
            {
                TempData["ErrorMessage"] = "Rental not found.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.OwnerId != user.Id && rental.RenterId != user.Id)
            {
                TempData["ErrorMessage"] = "You are not authorized to cancel this rental.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.Status == RentalStatus.Completed)
            {
                TempData["ErrorMessage"] = "Completed rentals cannot be cancelled.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            rental.Status = RentalStatus.Cancelled;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            // Notify renter that rental was cancelled
            await _hubContext.Clients.User(rental.RenterId!).SendAsync("RentalStatusChanged", new
            {
                rentalId = rental.Id,
                newStatus = rental.Status.ToString(),
                itemTitle = rental.Item.Title,
                message = $"Rental for '{rental.Item.Title}' was cancelled."
            });
            TempData["SuccessMessage"] = "Rental cancelled successfully.";
            return RedirectToAction("UserDashboard", "Dashboard");
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


