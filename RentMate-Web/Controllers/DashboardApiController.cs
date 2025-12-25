using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models; // Za ApplicationUser v bazi
using RentMate.Shared; // Za Item, Rental, DashboardViewModelDto
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace RentMate.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DashboardApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RentMateContext _context;

        public DashboardApiController(UserManager<ApplicationUser> userManager, RentMateContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("userdashboard")]
        public async Task<IActionResult> GetUserDashboard()
        {
            

            // Ker smo v Program.cs očistili mapiranje, bo "sub" vseboval ID
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                        ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            Console.WriteLine($"[DEBUG API] Končni popravljeni UserId: '{userId}'");

            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            // 2. Pridobivanje podatkov s "Shared" modeli za mobilno aplikacijo
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

            // 3. Sestava končnega DTO-ja
            var response = new DashboardViewModelDto
            {
                TotalListingsOwned = listingsOwned.Count,
                TotalRentalsAsRenter = myRentals.Count,
                TotalRentalsAsOwner = ownerRentalsCount,
                
                ListingsOwned = listingsOwned,
                // Za MyRentals moramo paziti na tip List<Rental> v DTO-ju
                MyRentals = myRentals.Cast<RentMate.Shared.Rental>().ToList() 
            };

            return Ok(response);
        }
        [HttpGet("my-rentals")]
        public async Task<IActionResult> GetMyRentals()
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Najemi, kjer sem JAZ tisti, ki si je sposodil (Renter)
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

            // Najemi, kjer so si DRUGI sposodili MOJO opremo (Owner)
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