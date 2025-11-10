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

        // ===========================
        // GET: api/Reviews/item/5?page=1&pageSize=10
        // ===========================
        [AllowAnonymous]
        [HttpGet("item/{itemId}")]
        public async Task<IActionResult> GetItemReviews(int itemId, int page = 1, int pageSize = 10)
        {
            var query = _context.Reviews
                .Include(r => r.Reviewer)
                .Where(r => r.ItemId == itemId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt);

            var total = await query.CountAsync();
            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Body,
                    r.Rating,
                    r.CreatedAt,
                    r.UpdatedAt,
                    r.IsAnonymous,
                    Reviewer = r.IsAnonymous
                        ? null
                        : new { r.Reviewer.Id, r.Reviewer.UserName }
                })
                .ToListAsync();

            return Ok(new { total, reviews });
        }

        // ===========================
        // GET: api/Reviews/mine/item/5
        // Used for editing or checking if a user already reviewed an item
        // ===========================
        [HttpGet("mine/item/{itemId}")]
        public async Task<IActionResult> GetMyReviewForItem(int itemId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var review = await _context.Reviews
                .Where(r => r.ItemId == itemId && r.ReviewerId == userId && !r.IsDeleted)
                .Select(r => new
                {
                    r.Id,
                    r.ItemId,
                    r.Title,
                    r.Body,
                    r.Rating,
                    r.IsAnonymous,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (review == null)
                return NotFound();

            return Ok(review);
        }

        // ===========================
        // POST: api/Reviews
        // Create a new review
        // ===========================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Review review)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var item = await _context.Items.FindAsync(review.ItemId);
            if (item == null) return NotFound("Item not found.");
            if (item.UserId == userId) return Forbid("Cannot review your own item.");

            // Check if the user has already reviewed this item
            var existingReview = await _context.Reviews
                .Where(r => r.ItemId == review.ItemId && r.ReviewerId == userId && !r.IsDeleted)
                .FirstOrDefaultAsync();
            if (existingReview != null)
            {
                return BadRequest("You have already reviewed this item.");
            }

            // Continue with normal review creation logic
            review.ReviewerId = userId;
            review.CreatedAt = DateTime.UtcNow;

            using var tx = await _context.Database.BeginTransactionAsync();
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Update item aggregates
            var all = _context.Reviews.Where(r => r.ItemId == review.ItemId && !r.IsDeleted);
            var count = await all.CountAsync();
            var avg = await all.AverageAsync(r => (double)r.Rating);

            item.ReviewCount = count;
            item.AverageRating = Math.Round(avg, 2);
            await _context.SaveChangesAsync();

            await tx.CommitAsync();

            return CreatedAtAction(nameof(GetItemReviews), new { itemId = review.ItemId }, review);
        }


        // ===========================
        // PUT: api/Reviews/5
        // Edit existing review
        // ===========================
        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] Review updated)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var review = await _context.Reviews.FindAsync(id);
            if (review == null || review.IsDeleted) return NotFound("Review not found.");
            if (review.ReviewerId != userId) return Forbid("You can only edit your own reviews.");

            review.Title = updated.Title;
            review.Body = updated.Body;
            review.Rating = updated.Rating;
            review.IsAnonymous = updated.IsAnonymous;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await UpdateItemAggregates(review.ItemId);

            return Ok(new
            {
                review.Id,
                review.Title,
                review.Body,
                review.Rating,
                review.UpdatedAt
            });
        }

        // ===========================
        // DELETE: api/Reviews/5
        // Soft delete a review
        // ===========================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var review = await _context.Reviews.FindAsync(id);
            if (review == null || review.IsDeleted) return NotFound("Review not found.");
            if (review.ReviewerId != userId && !User.IsInRole("Admin"))
                return Forbid("Not allowed to delete this review.");

            review.IsDeleted = true;
            await _context.SaveChangesAsync();
            await UpdateItemAggregates(review.ItemId);

            return NoContent();
        }

        // ===========================
        // Helper: Update averages after review changes
        // ===========================
        private async Task UpdateItemAggregates(int itemId)
        {
            var item = await _context.Items.FindAsync(itemId);
            if (item == null) return;

            var activeReviews = await _context.Reviews
                .Where(r => r.ItemId == itemId && !r.IsDeleted)
                .ToListAsync();

            if (activeReviews.Count == 0)
            {
                item.ReviewCount = 0;
                item.AverageRating = null;
            }
            else
            {
                item.ReviewCount = activeReviews.Count;
                item.AverageRating = Math.Round(activeReviews.Average(r => (double)r.Rating), 2);
            }

            await _context.SaveChangesAsync();
        }
    }
}
