using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models; // For ApplicationUser in the database
using RentMate.Shared; // For Item, Rental, DashboardViewModelDto
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Localization;

namespace RentMate.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DashboardApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RentMateContext _context;
        private readonly IStringLocalizer<DashboardApiController> _localizer;

        public DashboardApiController(
            UserManager<ApplicationUser> userManager, 
            RentMateContext context,
            IStringLocalizer<DashboardApiController> localizer)
        {
            _userManager = userManager;
            _context = context;
            _localizer = localizer;
        }

        [HttpGet("userdashboard")]
        public async Task<IActionResult> GetUserDashboard()
        {
            // Since mapping was cleared in Program.cs, "sub" will contain the ID
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                        ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Refactored to English log message
            Console.WriteLine($"[DEBUG API] Final corrected UserId: '{userId}'");

            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // 2. Fetching data with "Shared" models for mobile application
            var listingsOwned = await _context.Items
                .Where(i => i.UserId == userId)
                .Select(i => new RentMate.Shared.Item 
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description,
                    Price = i.Price,
                    Category = i.Category,
                    Location = i.Location,
                    IsListed = i.IsListed,
                    UserId = i.UserId
                }).ToListAsync();

            var myRentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.RenterId == userId)
                .Select(r => new RentMate.Shared.RentalDto
                {
                    Id = r.Id,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    TotalPrice = r.TotalPrice,
                    Status = (RentMate.Shared.RentalStatus)r.Status,
                    Item = new RentMate.Shared.ItemDto { Title = r.Item.Title }
                }).ToListAsync();

            var ownerRentalsCount = await _context.Rentals.CountAsync(r => r.OwnerId == userId);

            // 3. Construct the final DTO
            var response = new DashboardViewModelDto
            {
                TotalListingsOwned = listingsOwned.Count,
                TotalRentalsAsRenter = myRentals.Count,
                TotalRentalsAsOwner = ownerRentalsCount,
                
                ListingsOwned = listingsOwned,
                // Handle Rental casting for the DTO List
                MyRentals = myRentals.Cast<RentMate.Shared.Rental>().ToList() 
            };

            return Ok(response);
        }

        [HttpGet("my-rentals")]
        public async Task<IActionResult> GetMyRentals()
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Rentals where I am the Renter
            var rentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.RenterId == userId)
                .Select(r => new RentalDto
                {
                    Id = r.Id,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    TotalPrice = r.TotalPrice,
                    Status = (RentMate.Shared.RentalStatus)r.Status,
                    Item = new ItemDto { Title = r.Item.Title }
                }).ToListAsync();

            return Ok(rentals);
        }

        [HttpGet("owner-rentals")]
        public async Task<IActionResult> GetOwnerRentals()
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Rentals where others borrowed MY equipment (Owner)
            var rentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.OwnerId == userId)
                .Select(r => new RentalDto
                {
                    Id = r.Id,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    TotalPrice = r.TotalPrice,
                    Status = (RentMate.Shared.RentalStatus)r.Status,
                    Item = new ItemDto { Title = r.Item.Title }
                }).ToListAsync();

            return Ok(rentals);
        }
    }
}