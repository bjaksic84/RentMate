using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;

namespace RentMate.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(RentMateContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/Reviews/item/5?page=1&pageSize=10
        [AllowAnonymous]
        [HttpGet("item/{itemId}")]
        public async Task<IActionResult> GetItemReviews(int itemId, int page = 1, int pageSize = 10)
        {
            var query = _context.Reviews
                .Include(r => r.Reviewer)
                .Where(r => r.ItemId == itemId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt);

            var total = await query.CountAsync();
            var reviews = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new { total, reviews });
        }

        // POST: api/Reviews
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Review review)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var item = await _context.Items.FindAsync(review.ItemId);
            if (item == null) return NotFound("Item not found.");
            if (item.UserId == userId) return Forbid("Cannot review your own item.");

            // Check rental validity
            var validRental = await _context.Rentals.AnyAsync(r =>
                r.ItemId == review.ItemId &&
                r.RenterId == userId &&
                r.Status == RentalStatus.Completed);

            if (!validRental)
                return Forbid("You can only review items you've rented and completed.");

            // Prevent multiple reviews per rental (optional)
            bool alreadyReviewed = await _context.Reviews.AnyAsync(r =>
                r.ItemId == review.ItemId &&
                r.ReviewerId == userId &&
                !r.IsDeleted);

            if (alreadyReviewed)
                return BadRequest("You have already reviewed this item.");

            review.ReviewerId = userId;
            review.CreatedAt = DateTime.UtcNow;

            using var tx = await _context.Database.BeginTransactionAsync();
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Update aggregates
            var all = _context.Reviews.Where(r => r.ItemId == review.ItemId && !r.IsDeleted);
            var count = await all.CountAsync();
            var avg = await all.AverageAsync(r => (double)r.Rating);

            item.ReviewCount = count;
            item.AverageRating = Math.Round(avg, 2);
            await _context.SaveChangesAsync();

            await tx.CommitAsync();

            return CreatedAtAction(nameof(GetItemReviews), new { itemId = review.ItemId }, review);
        }

        // PUT: api/Reviews/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] Review updated)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var review = await _context.Reviews.FindAsync(id);
            if (review == null || review.IsDeleted) return NotFound();
            if (review.ReviewerId != userId) return Forbid();

            review.Title = updated.Title;
            review.Body = updated.Body;
            review.Rating = updated.Rating;
            review.UpdatedAt = DateTime.UtcNow;

            using var tx = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();

            // Recalculate aggregates
            var all = _context.Reviews.Where(r => r.ItemId == review.ItemId && !r.IsDeleted);
            var count = await all.CountAsync();
            var avg = await all.AverageAsync(r => (double)r.Rating);

            var item = await _context.Items.FindAsync(review.ItemId);
            if (item != null)
            {
                item.ReviewCount = count;
                item.AverageRating = Math.Round(avg, 2);
                await _context.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return Ok(review);
        }

        // DELETE: api/Reviews/5 (soft delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var review = await _context.Reviews.FindAsync(id);
            if (review == null || review.IsDeleted) return NotFound();

            // Allow author or admin
            if (review.ReviewerId != userId && !User.IsInRole("Admin"))
                return Forbid();

            review.IsDeleted = true;
            await _context.SaveChangesAsync();

            // Update aggregate
            var item = await _context.Items.FindAsync(review.ItemId);
            if (item != null)
            {
                var all = _context.Reviews.Where(r => r.ItemId == item.Id && !r.IsDeleted);
                item.ReviewCount = await all.CountAsync();
                item.AverageRating = item.ReviewCount == 0 ? (double?)null : await all.AverageAsync(r => (double)r.Rating);
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }
    }
}