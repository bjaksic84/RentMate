using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using Microsoft.AspNetCore.SignalR;
using RentMate.Hubs;
using RentMate.Shared;
using Microsoft.Extensions.Localization;

namespace RentMate.Controllers
{
    [Authorize]
    public class RentalsController : Controller
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<RentMateHub> _hubContext;
        private readonly IStringLocalizer<RentalsController> _localizer;

        public RentalsController(
            RentMateContext context, 
            UserManager<ApplicationUser> userManager, 
            IHubContext<RentMateHub> hubContext,
            IStringLocalizer<RentalsController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _localizer = localizer;
        }

        // 🔹 Public listings: items available to rent
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? search,
            decimal? minPrice,
            decimal? maxPrice,
            string? city,
            DateTime? startDate,
            DateTime? endDate,
            string? sort)
        {
            // Base query: only listed and not rented
            var query = _context.Items
                .Include(i => i.User)
                .Include(i => i.Rentals)
                .Where(i => i.IsListed)
                .AsQueryable();

            // 🔍 Text search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                query = query.Where(i =>
                    (i.Title != null && i.Title.ToLower().Contains(lower)) ||
                    (i.Description != null && i.Description.ToLower().Contains(lower)));
            }

            // 💶 Price filters
            if (minPrice.HasValue)
                query = query.Where(i => i.Price >= minPrice.Value);
            if (maxPrice.HasValue)
                query = query.Where(i => i.Price <= maxPrice.Value);

            // 📍 City filter
            if (!string.IsNullOrEmpty(city))
                query = query.Where(i => i.User!.City == city);

            // 🗓️ Availability filter (only if both dates given)
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(i =>
                    !i.Rentals!.Any(r =>
                        (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending) &&
                        r.StartDate <= endDate && r.EndDate >= startDate));
            }

            // ⚙️ Sorting
            query = sort switch
            {
                "priceAsc" => query.OrderBy(i => i.Price),
                "priceDesc" => query.OrderByDescending(i => i.Price),
                "titleAsc" => query.OrderBy(i => i.Title),
                _ => query.OrderByDescending(i => i.CreatedAt) // Default: newest first
            };

            // Execute query
            var available = await query.ToListAsync();

            // Populate dropdown data — use canonical list from CityData
            var cities = RentMate.Helpers.CityData.Cities.Select(c => c.Name).ToList();

            // Pass current filters to view (to persist values)
            ViewBag.Search = search;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.City = city;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Sort = sort;
            ViewBag.Cities = cities;

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
                var msg = _localizer["Item not available for rent."].Value;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(msg);

                TempData["ErrorMessage"] = msg;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (item.UserId == user.Id)
            {
                var msg = _localizer["You cannot rent your own item."].Value;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(msg);

                TempData["ErrorMessage"] = msg;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            // Prevent overlapping rentals
            bool hasConflict = item.Rentals!.Any(r =>
                (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending) &&
                r.StartDate <= endDate &&
                r.EndDate >= startDate);

            if (hasConflict)
            {
                var msg = _localizer["Item is already booked during this period."].Value;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(msg);

                TempData["ErrorMessage"] = msg;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            // Calculate total price
            int rentalDays = Math.Max((endDate.Date - startDate.Date).Days, 1);
            decimal totalPrice = (item.Price ?? 0) * rentalDays;

            var rental = new RentMate.Models.Rental
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

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = _localizer["Rental request submitted successfully."].Value });

            TempData["SuccessMessage"] = _localizer["Rental request submitted. Awaiting owner approval."].Value;
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
                TempData["ErrorMessage"] = _localizer["Rental not found."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.OwnerId != user.Id)
            {
                TempData["ErrorMessage"] = _localizer["You are not authorized to approve this rental."].Value;
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
                message = string.Format(_localizer["Your rental request for '{0}' was approved!"], rental.Item.Title)
            });

            TempData["SuccessMessage"] = string.Format(_localizer["You approved rental for '{0}'."], rental.Item.Title);
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
                TempData["ErrorMessage"] = _localizer["Rental not found."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.OwnerId != user.Id && rental.RenterId != user.Id)
            {
                TempData["ErrorMessage"] = _localizer["You are not authorized to complete this rental."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            rental.Status = RentalStatus.Completed;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;
            rental.EndDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _hubContext.Clients.User(rental.RenterId!).SendAsync("RentalStatusChanged", new
            {
                rentalId = rental.Id,
                newStatus = rental.Status.ToString(),
                itemTitle = rental.Item.Title,
                message = string.Format(_localizer["Rental for '{0}' was marked as completed."], rental.Item.Title)
            });

            TempData["SuccessMessage"] = string.Format(_localizer["Rental for '{0}' completed successfully."], rental.Item.Title);
            return RedirectToAction("UserDashboard", "Dashboard");
        }

        // 🔹 Step 3b: Cancel rental (either party)
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
                TempData["ErrorMessage"] = _localizer["Rental not found."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.OwnerId != user.Id && rental.RenterId != user.Id)
            {
                TempData["ErrorMessage"] = _localizer["You are not authorized to cancel this rental."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            if (rental.Status == RentalStatus.Completed)
            {
                TempData["ErrorMessage"] = _localizer["Completed rentals cannot be cancelled."].Value;
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            rental.Status = RentalStatus.Cancelled;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _hubContext.Clients.User(rental.RenterId!).SendAsync("RentalStatusChanged", new
            {
                rentalId = rental.Id,
                newStatus = rental.Status.ToString(),
                itemTitle = rental.Item!.Title,
                message = string.Format(_localizer["Rental for '{0}' was cancelled."], rental.Item!.Title)
            });

            TempData["SuccessMessage"] = _localizer["Rental cancelled successfully."].Value;
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