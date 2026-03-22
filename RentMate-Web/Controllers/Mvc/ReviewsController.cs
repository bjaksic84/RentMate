using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Services.Interfaces;
using RentMate.Services.Extensions;
using RentMate.Services.Implementations;
using Microsoft.Extensions.Localization;

namespace RentMate.Controllers.Mvc
{
    /// <summary>
    /// API controller for review operations (cookie-based authentication for web frontend).
    /// Note: This is different from ReviewApiController which uses JWT for mobile apps.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    [AutoValidateAntiforgeryToken]
    public class ReviewsController : ControllerBase
    {
        #region Constants

        private const int DefaultPageSize = 10;
        private const string AdminRole = "Admin";

        #endregion

        #region Dependencies

        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<ReviewsController> _localizer;
        private readonly IReviewAggregationService _reviewAggregation;
        private readonly IScoringService _scoringService;
        private readonly INotificationService _notificationService;

        #endregion

        #region Constructor

        public ReviewsController(
            RentMateContext context,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<ReviewsController> localizer,
            IReviewAggregationService reviewAggregation,
            IScoringService scoringService,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
            _reviewAggregation = reviewAggregation;
            _scoringService = scoringService;
            _notificationService = notificationService;
        }

        #endregion

        #region Read Operations

        /// <summary>
        /// Gets paginated reviews for a specific item.
        /// GET: api/Reviews/item/5?page=1&pageSize=10
        /// </summary>
        [AllowAnonymous]
        [HttpGet("item/{itemId}")]
        public async Task<IActionResult> GetItemReviews(int itemId, int page = 1, int pageSize = DefaultPageSize)
        {
            var query = BuildItemReviewsQuery(itemId);

            var total = await query.CountAsync();
            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => MapToReviewDto(r))
                .ToListAsync();

            return Ok(new { total, reviews });
        }

        /// <summary>
        /// Gets the current user's review for a specific item.
        /// GET: api/Reviews/mine/item/5
        /// </summary>
        [HttpGet("mine/item/{itemId}")]
        public async Task<IActionResult> GetMyReviewForItem(int itemId)
        {
            var userId = GetCurrentUserId();
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

            return review == null ? NotFound() : Ok(review);
        }

        #endregion

        #region Create/Update Operations

        /// <summary>
        /// Creates a new review for an item.
        /// POST: api/Reviews
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReviewMvcRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest("Rating must be between 1 and 5.");

            // Validate item exists and user isn't the owner
            var validationResult = await ValidateCreateReviewAsync(request.ItemId, userId);
            if (validationResult != null) return validationResult;

            // Check for duplicate review
            if (await HasExistingReviewAsync(request.ItemId, userId))
            {
                return BadRequest(_localizer["You have already reviewed this item."].Value);
            }

            var review = new Review
            {
                ItemId = request.ItemId,
                Rating = request.Rating,
                Title = request.Title,
                Body = request.Body,
                IsAnonymous = request.IsAnonymous,
                ReviewerId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            await _reviewAggregation.UpdateItemAggregatesAsync(review.ItemId);

            // Notify the item owner about the new review
            var notifItem = await _context.Items.FindAsync(request.ItemId);
            if (notifItem != null)
            {
                var reviewer = await _context.Users.FindAsync(userId);
                await _notificationService.CreateAsync(
                    notifItem.UserId!, NotificationType.ReviewReceived,
                    _localizer["Notification.ReviewReceived"].Value,
                    string.Format(_localizer["NotificationMsg.ReviewReceived"].Value,
                        reviewer?.FirstName ?? reviewer?.UserName ?? "",
                        request.Rating, notifItem.Title),
                    review.Id, "Review",
                    $"/Items/Details/{notifItem.Id}#reviews");
            }

            // Event-driven: recompute item score after new review
            _ = Task.Run(() => _scoringService.ComputeAndSaveItemScoreAsync(review.ItemId));

            return CreatedAtAction(nameof(GetItemReviews), new { itemId = review.ItemId }, new { review.Id, review.Rating, review.Title, review.Body });
        }

        /// <summary>
        /// Updates an existing review.
        /// PUT: api/Reviews/5
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] EditReviewMvcRequest updated)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (updated.Rating < 1 || updated.Rating > 5)
                return BadRequest("Rating must be between 1 and 5.");

            var review = await FindActiveReviewAsync(id);
            if (review == null) return NotFound(_localizer["Review not found."].Value);
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

        #endregion

        #region Delete Operations

        /// <summary>
        /// Soft deletes a review.
        /// DELETE: api/Reviews/5
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var review = await FindActiveReviewAsync(id);
            if (review == null) return NotFound(_localizer["Review not found."].Value);
            
            // Only allow deletion by owner or admin
            if (!CanDeleteReview(review, userId))
            {
                return Forbid();
            }

            review.IsDeleted = true;
            await _context.SaveChangesAsync();
            await _reviewAggregation.UpdateItemAggregatesAsync(review.ItemId);

            return NoContent();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Gets the current user's ID.
        /// </summary>
        private string? GetCurrentUserId() => _userManager.GetUserId(User);

        /// <summary>
        /// Builds query for item reviews ordered by date.
        /// </summary>
        private IQueryable<Review> BuildItemReviewsQuery(int itemId)
        {
            return _context.Reviews
                .Include(r => r.Reviewer)
                .Where(r => r.ItemId == itemId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt);
        }

        /// <summary>
        /// Maps a review entity to a DTO for API response.
        /// </summary>
        private static object MapToReviewDto(Review r) => new
        {
            r.Id,
            r.Title,
            r.Body,
            r.Rating,
            r.CreatedAt,
            r.UpdatedAt,
            r.IsAnonymous,
            Reviewer = r.IsAnonymous || r.Reviewer == null
                ? null
                : new { r.Reviewer.Id, r.Reviewer.UserName }
        };

        /// <summary>
        /// Validates that a review can be created.
        /// </summary>
        private async Task<IActionResult?> ValidateCreateReviewAsync(int itemId, string userId)
        {
            var item = await _context.Items.FindAsync(itemId);
            
            if (item == null)
            {
                return NotFound(_localizer["Item not found."].Value);
            }
            
            if (item.UserId == userId)
            {
                return Forbid(); // Cannot review own item
            }

            return null;
        }

        /// <summary>
        /// Checks if user has already reviewed the item.
        /// </summary>
        private Task<bool> HasExistingReviewAsync(int itemId, string userId)
        {
            return _context.Reviews
                .AnyAsync(r => r.ItemId == itemId && r.ReviewerId == userId && !r.IsDeleted);
        }

        /// <summary>
        /// Finds an active (non-deleted) review by ID.
        /// </summary>
        private async Task<Review?> FindActiveReviewAsync(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            return review?.IsDeleted == false ? review : null;
        }

        /// <summary>
        /// Checks if the current user can delete the review.
        /// </summary>
        private bool CanDeleteReview(Review review, string userId)
        {
            return review.ReviewerId == userId || User.IsInRole(AdminRole);
        }

        #endregion
    }
}

/// <summary>DTO for creating a review via the MVC endpoint.</summary>
public record CreateReviewMvcRequest(int ItemId, int Rating, string? Title, string? Body, bool IsAnonymous);

/// <summary>DTO for editing a review via the MVC endpoint.</summary>
public record EditReviewMvcRequest(int Rating, string? Title, string? Body, bool IsAnonymous);

