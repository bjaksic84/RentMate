using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Services.Interfaces;
using RentMate.Services.Extensions;
using RentMate.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using RentMate.Shared.Contracts.Requests;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Controllers.Api
{
    /// <summary>
    /// API controller for review operations.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("ApiPolicy")]
    [Produces("application/json")]
    public class ReviewApiController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReviewApiController> _logger;
        private readonly IReviewAggregationService _reviewAggregation;
        private readonly IScoringService _scoringService;

        public ReviewApiController(
            RentMateContext context, 
            UserManager<ApplicationUser> userManager, 
            ILogger<ReviewApiController> logger,
            IReviewAggregationService reviewAggregation,
            IScoringService scoringService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _reviewAggregation = reviewAggregation;
            _scoringService = scoringService;
        }

        /// <summary>
        /// Create or update a review for a rental.
        /// </summary>
        /// <param name="request">The review request details.</param>
        /// <returns>Success indicator.</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> PostReview(CreateReviewRequest request)
        {
            _logger.LogInformation("Incoming POST /api/reviewapi payload: Rating={Rating}, ItemId={ItemId}, RentalId={RentalId}", request.Rating, request.ItemId, request.RentalId);

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Verify user can only review if they completed this rental
            if (request.RentalId.HasValue)
            {
                var rental = await _context.Rentals.FindAsync(request.RentalId.Value);
                if (rental == null || rental.RenterId != userId)
                {
                    return Forbid("You do not have permission to review this rental.");
                }
            }

            // Validate Item exists
            if (request.ItemId <= 0)
            {
                _logger.LogWarning("Invalid ItemId in incoming review: {ItemId}", request.ItemId);
                return BadRequest(new { error = "Invalid ItemId" });
            }

            var itemExists = await _context.Items.AnyAsync(i => i.Id == request.ItemId);
            if (!itemExists)
            {
                _logger.LogWarning("Attempt to create review for non-existent ItemId {ItemId}", request.ItemId);
                return BadRequest(new { error = "Item not found" });
            }

            // If an existing review by this user for this rental exists, update it
            Review? existing = null;
            if (request.RentalId.HasValue)
            {
                existing = await _context.Reviews.FirstOrDefaultAsync(x => x.RentalId == request.RentalId.Value && x.ReviewerId == userId && !x.IsDeleted);
            }

            if (existing != null)
            {
                existing.Rating = request.Rating;
                existing.Body = request.Body;
                existing.UpdatedAt = DateTime.UtcNow;
                _context.Reviews.Update(existing);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated existing review {ReviewId} by user {UserId}", existing.Id, userId);
                await _reviewAggregation.UpdateItemAggregatesAsync(existing.ItemId);
                _ = Task.Run(() => _scoringService.ComputeAndSaveItemScoreAsync(existing.ItemId));
                return Ok(new { updated = true });
            }

            // Create new review
            var dbReview = new Review
            {
                ItemId = request.ItemId,
                RentalId = request.RentalId,
                Rating = request.Rating,
                Body = request.Body,
                ReviewerId = userId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Reviews.Add(dbReview);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new review {ReviewId} for Item {ItemId} by user {UserId}", dbReview.Id, dbReview.ItemId, userId);

            await _reviewAggregation.UpdateItemAggregatesAsync(dbReview.ItemId);

            // Event-driven: recompute item score after new review
            _ = Task.Run(() => _scoringService.ComputeAndSaveItemScoreAsync(dbReview.ItemId));

            return Ok(new { created = true });
        }

        /// <summary>
        /// Get all reviews for a specific item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>List of review summaries.</returns>
        [HttpGet("item/{itemId}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<ReviewSummary>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ReviewSummary>>> GetItemReviews(int itemId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.ItemId == itemId && !r.IsDeleted)
                .Include(r => r.Reviewer)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewSummary(
                    r.Id,
                    r.Rating,
                    r.Body,
                    r.CreatedAt,
                    r.Reviewer != null ? r.Reviewer.UserName ?? "Anonymous" : "Anonymous",
                    r.Reviewer != null ? r.Reviewer.ProfilePictureUrl : null
                ))
                .ToListAsync();

            return Ok(reviews);
        }

        /// <summary>
        /// Update an existing review.
        /// </summary>
        /// <param name="id">The review ID.</param>
        /// <param name="request">The update request.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateReview(int id, UpdateReviewRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var review = await _context.Reviews.FindAsync(id);
            if (review == null || review.IsDeleted) return NotFound();
            if (review.ReviewerId != userId) return Forbid();

            review.Rating = request.Rating;
            review.Body = request.Body;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _reviewAggregation.UpdateItemAggregatesAsync(review.ItemId);

            // Event-driven: recompute item score after review update
            _ = Task.Run(() => _scoringService.ComputeAndSaveItemScoreAsync(review.ItemId));

            return NoContent();
        }

        /// <summary>
        /// Delete a review (soft delete).
        /// </summary>
        /// <param name="id">The review ID.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var review = await _context.Reviews.FindAsync(id);
            if (review == null || review.IsDeleted) return NotFound();
            if (review.ReviewerId != userId) return Forbid();

            review.IsDeleted = true;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _reviewAggregation.UpdateItemAggregatesAsync(review.ItemId);

            // Event-driven: recompute item score after review deletion
            _ = Task.Run(() => _scoringService.ComputeAndSaveItemScoreAsync(review.ItemId));

            return NoContent();
        }
    }
}

