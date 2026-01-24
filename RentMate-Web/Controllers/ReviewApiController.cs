using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using RentMate.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace RentMate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("ApiPolicy")]
    public class ReviewApiController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReviewApiController> _logger;

        public ReviewApiController(RentMateContext context, UserManager<ApplicationUser> userManager, ILogger<ReviewApiController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> PostReview(RentMate.Shared.Review review)
        {
            _logger.LogInformation("Incoming POST /api/reviewapi payload: Rating={Rating}, ItemId={ItemId}, RentalId={RentalId}", review?.Rating, review?.ItemId, review?.RentalId);
            var modelErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray();
            if (modelErrors.Length > 0)
            {
                _logger.LogWarning("ModelState BEFORE removals has {Count} errors: {@Errors}", modelErrors.Length, modelErrors);
            }

            ModelState.Remove(nameof(review.ReviewerId));

            modelErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray();
            if (modelErrors.Length > 0)
            {
                _logger.LogWarning("ModelState AFTER removing ReviewerId has {Count} errors: {@Errors}", modelErrors.Length, modelErrors);
            }

            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Preverjanje: Uporabnik lahko oceni le, če je on opravil ta najem
            if (review.RentalId.HasValue)
            {
                var rental = await _context.Rentals.FindAsync(review.RentalId.Value);
                if (rental == null || rental.RenterId != userId)
                {
                    return Forbid("Nimate dovoljenja za ocenjevanje tega najema.");
                }
            }

            // Validate Item exists (prevents FK constraint failure)
            if (review.ItemId <= 0)
            {
                _logger.LogWarning("Invalid ItemId in incoming review: {ItemId}", review?.ItemId);
                return BadRequest(new { error = "Invalid ItemId" });
            }

            var itemExists = await _context.Items.AnyAsync(i => i.Id == review.ItemId);
            if (!itemExists)
            {
                _logger.LogWarning("Attempt to create review for non-existent ItemId {ItemId}", review.ItemId);
                return BadRequest(new { error = "Item not found" });
            }

            // If an existing review by this user for this rental exists, update it instead of inserting
            RentMate.Models.Review? existing = null;
            if (review.RentalId.HasValue)
            {
                existing = await _context.Reviews.FirstOrDefaultAsync(x => x.RentalId == review.RentalId.Value && x.ReviewerId == userId && !x.IsDeleted);
            }

            if (existing != null)
            {
                existing.Rating = review.Rating;
                existing.Title = review.Title;
                existing.Body = review.Body;
                existing.UpdatedAt = DateTime.UtcNow;
                _context.Reviews.Update(existing);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated existing review {ReviewId} by user {UserId}", existing.Id, userId);
                // Update item aggregates
                await UpdateItemAggregates(existing.ItemId);
                return Ok(new { updated = true });
            }

            // Ustvarimo bazični model (Models.Review)
            var dbReview = new RentMate.Models.Review
            {
                ItemId = review.ItemId,
                RentalId = review.RentalId,
                Rating = review.Rating,
                Title = review.Title,
                Body = review.Body,
                ReviewerId = userId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Reviews.Add(dbReview);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new review {ReviewId} for Item {ItemId} by user {UserId}", dbReview.Id, dbReview.ItemId, userId);

            // Update item aggregates
            await UpdateItemAggregates(dbReview.ItemId);

            return Ok(new { created = true });
        }

        // Helper: Update averages after review changes (duplicated from ReviewsController)
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

        [HttpGet("item/{itemId}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ReviewDto>>> GetItemReviews(int itemId)
        {
            return await _context.Reviews
                .Where(r => r.ItemId == itemId && !r.IsDeleted)
                .Include(r => r.Reviewer)
                .Select(r => new ReviewDto 
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Title = r.Title,
                    Body = r.Body,
                    CreatedAt = r.CreatedAt,
                    Reviewer = new UserDto { UserName = r.ReviewerId } // Ali pa pravi join na User tabelo
                })
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}