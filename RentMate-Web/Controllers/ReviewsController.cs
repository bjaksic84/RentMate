using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using RentMate.Services;
using Microsoft.Extensions.Localization;

namespace RentMate.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<ReviewsController> _localizer;
        private readonly IReviewAggregationService _reviewAggregation;

        public ReviewsController(
            RentMateContext context, 
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<ReviewsController> localizer,
            IReviewAggregationService reviewAggregation)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
            _reviewAggregation = reviewAggregation;
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
            if (item == null) return NotFound(_localizer["Item not found."].Value);
            if (item.UserId == userId) return Forbid(); // Contextually clear without string

            // Check if the user has already reviewed this item
            var existingReview = await _context.Reviews
                .Where(r => r.ItemId == review.ItemId && r.ReviewerId == userId && !r.IsDeleted)
                .FirstOrDefaultAsync();
                
            if (existingReview != null)
            {
                return BadRequest(_localizer["You have already reviewed this item."].Value);
            }

            review.ReviewerId = userId;
            review.CreatedAt = DateTime.UtcNow;

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Update item aggregates using the centralized service
            await _reviewAggregation.UpdateItemAggregatesAsync(review.ItemId);

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
            if (review == null || review.IsDeleted) return NotFound(_localizer["Review not found."].Value);
            if (review.ReviewerId != userId) return Forbid();

            review.Title = updated.Title;
            review.Body = updated.Body;
            review.Rating = updated.Rating;
            review.IsAnonymous = updated.IsAnonymous;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _reviewAggregation.UpdateItemAggregatesAsync(review.ItemId);

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
            if (review == null || review.IsDeleted) return NotFound(_localizer["Review not found."].Value);
            if (review.ReviewerId != userId && !User.IsInRole("Admin"))
                return Forbid();

            review.IsDeleted = true;
            await _context.SaveChangesAsync();
            await _reviewAggregation.UpdateItemAggregatesAsync(review.ItemId);

            return NoContent();
        }
    }
}