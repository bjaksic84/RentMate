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
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Fetch rentals with a left join to reviews (avoids N+1 query)
            var rentalsWithReviews = await (
                from r in _context.Rentals
                where r.RenterId == userId
                join rv in _context.Reviews.Where(rv => rv.ReviewerId == userId && !rv.IsDeleted)
                    on r.Id equals rv.RentalId into reviews
                from review in reviews.DefaultIfEmpty()
                select new {
                    Rental = r,
                    r.Item,
                    ItemUser = r.Item != null ? r.Item.User : null,
                    Review = review
                }
            ).ToListAsync();

            var result = rentalsWithReviews.Select(x => new RentalDto {
                Id = x.Rental.Id,
                ItemId = x.Rental.ItemId,
                Item = x.Item != null ? new ItemDto { Id = x.Item.Id, Title = x.Item.Title } : null,
                StartDate = x.Rental.StartDate,
                EndDate = x.Rental.EndDate,
                TotalPrice = x.Rental.TotalPrice,
                Status = x.Rental.Status,
                Owner = x.ItemUser != null ? new UserDto { UserName = x.ItemUser.UserName } : null,
                ExistingReview = x.Review != null ? new ReviewDto { 
                    Id = x.Review.Id, 
                    Rating = x.Review.Rating, 
                    Title = x.Review.Title, 
                    Body = x.Review.Body, 
                    CreatedAt = x.Review.CreatedAt 
                } : null
            }).ToList();

            return Ok(result);
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