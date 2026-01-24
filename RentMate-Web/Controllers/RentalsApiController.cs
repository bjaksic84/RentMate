using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using RentMate.Shared; // Uporaba Shared modelov

namespace RentMate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("ApiPolicy")]
    public class RentalsApiController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<RentMate.Models.ApplicationUser> _userManager;

        public RentalsApiController(RentMateContext context, UserManager<RentMate.Models.ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        public async Task<ActionResult<Rental>> PostRental(Rental rental)
        {
            var renterId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(renterId)) return Unauthorized();

            var item = await _context.Items.FindAsync(rental.ItemId);
            if (item == null) return NotFound("Predmet ne obstaja.");

            if (item.UserId == renterId) return BadRequest("Svojega izdelka ne morete rezervirati.");

            // Mapiranje na strežniški model
            var dbRental = new RentMate.Models.Rental
            {
                ItemId = rental.ItemId,
                RenterId = renterId,
                OwnerId = item.UserId,
                StartDate = rental.StartDate,
                EndDate = rental.EndDate,
                RentalDate = DateTime.Now,
                Status = RentalStatus.Pending
            };

            // Fix za Error CS0266 (decimal? -> decimal)
            int days = (dbRental.EndDate - dbRental.StartDate).Days;
            if (days <= 0) days = 1;
            dbRental.TotalPrice = (item.Price ?? 0m) * days;

            _context.Rentals.Add(dbRental);
            await _context.SaveChangesAsync();

            return Ok(rental);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] RentalStatus newStatus)
        {
            var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == id);
            if (rental == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (rental.Item.UserId != userId && rental.RenterId != userId) return Forbid();

            rental.Status = newStatus;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("my-rentals")]
        public async Task<ActionResult<IEnumerable<RentalDto>>> GetMyRentals() {
            var userId = _userManager.GetUserId(User);
            return await _context.Rentals
                .Where(r => r.RenterId == userId)
                .Select(r => new RentalDto {
                    Id = r.Id,
                    ItemId = r.ItemId,
                    Item = r.Item != null ? new ItemDto { Id = r.Item.Id, Title = r.Item.Title } : null,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status, // Enum se avtomatsko prenese
                    Owner = r.Item != null && r.Item.User != null ? new UserDto { UserName = r.Item.User.UserName } : null,
                    ExistingReview = _context.Reviews
                        .Where(rv => rv.RentalId == r.Id && rv.ReviewerId == userId && !rv.IsDeleted)
                        .Select(rv => new ReviewDto { Id = rv.Id, Rating = rv.Rating, Title = rv.Title, Body = rv.Body, CreatedAt = rv.CreatedAt })
                        .FirstOrDefault()
                }).ToListAsync();
        }

        [HttpGet("owner-rentals")]
        public async Task<ActionResult<IEnumerable<RentalDto>>> GetOwnerRentals() {
            var userId = _userManager.GetUserId(User);
            return await _context.Rentals
                .Where(r => r.OwnerId == userId)
                .Select(r => new RentalDto {
                    Id = r.Id,
                    ItemId = r.ItemId,
                    Item = r.Item != null ? new ItemDto { Id = r.Item.Id, Title = r.Item.Title } : null,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status,
                    Renter = r.Renter != null ? new UserDto { UserName = r.Renter.UserName } : null
                }).ToListAsync();
        }
    }
}