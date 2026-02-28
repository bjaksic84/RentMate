using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Shared.Contracts.Requests;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Controllers.Api
{
    /// <summary>
    /// API controller for item operations.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("ApiPolicy")]
    [Produces("application/json")]
    public class ItemsApiController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<ItemsApiController> _localizer;
        private readonly ILogger<ItemsApiController> _logger;

        public ItemsApiController(
            RentMateContext context, 
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<ItemsApiController> localizer,
            ILogger<ItemsApiController> logger)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
            _logger = logger;
        }

        /// <summary>
        /// Gets all listed items.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ItemWithOwner>>> GetItems()
        {
            // Map Web models from database to new shared contracts
            var items = await _context.Items
                .Include(i => i.User)
                .Include(i => i.Reviews)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder).Take(1))
                .Where(i => i.IsListed && !i.IsAdminHidden)
                .Select(i => new ItemWithOwner(
                    i.Id,
                    i.Title ?? "",
                    i.Description,
                    i.Price ?? 0,
                    i.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault() != null
                        ? i.Images.OrderBy(img => img.DisplayOrder).First().ImageUrl
                        : i.ImageUrl,
                    i.Location ?? i.User!.City,
                    i.Category,
                    i.IsListed,
                    i.IsAdminHidden,
                    i.Reviews.Any() ? i.Reviews.Average(r => r.Rating) : 0,
                    i.Reviews.Count,
                    i.CreatedAt,
                    i.UpdatedAt,
                    new UserSummary(
                        i.User!.Id,
                        i.User.UserName ?? "",
                        i.User.Email,
                        i.User.FirstName,
                        i.User.LastName,
                        i.User.City,
                        i.User.ProfilePictureUrl
                    )
                ))
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// Gets a specific item by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ItemWithOwner>> GetItem(int id)
        {
            var item = await _context.Items
                .Include(i => i.User)
                .Include(i => i.Reviews)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder))
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
                return NotFound();

            var itemWithOwner = new ItemWithOwner(
                item.Id,
                item.Title ?? "",
                item.Description,
                item.Price ?? 0,
                item.PrimaryImageUrl,
                item.Location ?? item.User?.City,
                item.Category,
                item.IsListed,
                item.IsAdminHidden,
                (item.Reviews?.Any() ?? false) ? item.Reviews.Average(r => r.Rating) : 0,
                item.Reviews?.Count ?? 0,
                item.CreatedAt,
                item.UpdatedAt,
                item.User != null ? new UserSummary(
                    item.User.Id,
                    item.User.UserName ?? "",
                    item.User.Email,
                    item.User.FirstName,
                    item.User.LastName,
                    item.User.City,
                    item.User.ProfilePictureUrl
                ) : null!
            );

            return Ok(itemWithOwner);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ItemWithOwner>> PostItem(CreateItemRequest request)
        {
            // 1. Attempt to get ID in two ways
            string? userId = _userManager.GetUserId(User);
            
            // If UserManager failed, try directly from Claims
            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }

            // 2. If still NULL, we cannot proceed to the database
            if (string.IsNullOrEmpty(userId))
            {
                // Return 401 so MAUI can see that the token was not read correctly
                return Unauthorized(_localizer["Error: Server cannot find your ID in the token. Dashboard will show 0."].Value);
            }

            // Government ID gate: must verify before listing items
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !user.IsGovernmentIdVerified)
            {
                return BadRequest("You must verify your government ID before listing items for rent.");
            }

            var newItem = new Item
            {
                Title = request.Title,
                Description = request.Description,
                Price = request.PricePerDay,
                IsListed = request.IsListed,
                Location = request.City,
                Category = request.Category,
                ImageUrl = request.ImageUrl,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.Items.Add(newItem);
                await _context.SaveChangesAsync();
                
                // Reload with user to return full ItemWithOwner
                await _context.Entry(newItem).Reference(i => i.User).LoadAsync();
                
                var result = new ItemWithOwner(
                    newItem.Id,
                    newItem.Title,
                    newItem.Description,
                    newItem.Price ?? 0,
                    newItem.ImageUrl,
                    newItem.Location ?? newItem.User?.City,
                    newItem.Category,
                    newItem.IsListed,
                    newItem.IsAdminHidden,
                    0, // New item has no reviews
                    0,
                    newItem.CreatedAt,
                    null,
                    new UserSummary(
                        newItem.User!.Id,
                        newItem.User.UserName ?? "",
                        newItem.User.Email,
                        newItem.User.FirstName,
                        newItem.User.LastName,
                        newItem.User.City,
                        newItem.User.ProfilePictureUrl
                    )
                );
                
                return CreatedAtAction("GetItem", new { id = newItem.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving item for user {UserId}", newItem.UserId);
                return BadRequest(_localizer["An error occurred while saving. Please try again."].Value);
            }
        }

        // PUT: api/Items/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(int id, UpdateItemRequest request)
        {
            // Get current user ID for authorization
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var webItem = await _context.Items.FindAsync(id);
            if (webItem == null) 
                return NotFound();

            // Authorization: Only the owner can modify their item
            if (webItem.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to modify item {ItemId} owned by {OwnerId}", 
                    userId, id, webItem.UserId);
                return Forbid();
            }

            // Update Web model values from request
            webItem.Title = request.Title;
            webItem.Description = request.Description;
            webItem.Price = request.PricePerDay;
            webItem.Location = request.City;
            webItem.Category = request.Category;
            webItem.ImageUrl = request.ImageUrl;
            webItem.IsListed = request.IsListed;
            webItem.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Items.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/Items/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            // Get current user ID for authorization
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var item = await _context.Items.FindAsync(id);
            if (item == null)
                return NotFound();

            // Authorization: Only the owner can delete their item
            if (item.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to delete item {ItemId} owned by {OwnerId}", 
                    userId, id, item.UserId);
                return Forbid();
            }

            _context.Items.Remove(item);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted item {ItemId}", userId, id);
            return NoContent();
        }

        /// <summary>
        /// Get items owned by the current user.
        /// </summary>
        /// <returns>List of item summaries.</returns>
        [HttpGet("myitems")]
        [ProducesResponseType(typeof(IEnumerable<ItemSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<ItemSummary>>> GetMyItems()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var items = await _context.Items
                .Where(i => i.UserId == userId)
                .Include(i => i.Reviews)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder).Take(1))
                .Select(i => new ItemSummary(
                    i.Id,
                    i.Title ?? "Untitled",
                    i.Description,
                    i.Price ?? 0,
                    i.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault() != null
                        ? i.Images.OrderBy(img => img.DisplayOrder).First().ImageUrl
                        : i.ImageUrl,
                    i.Location,
                    i.Category,
                    i.IsListed,
                    i.IsAdminHidden,
                    i.Reviews.Any() ? i.Reviews.Average(r => r.Rating) : 0,
                    i.Reviews.Count,
                    i.CreatedAt
                ))
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// Search items with optional filters.
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PaginatedResponse<ItemWithOwner>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedResponse<ItemWithOwner>>> SearchItems([FromBody] SearchItemsRequest request)
        {
            var query = _context.Items
                .Include(i => i.User)
                .Include(i => i.Reviews)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder).Take(1))
                .Where(i => i.IsListed && !i.IsAdminHidden);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.SearchQuery))
            {
                var searchTerm = request.SearchQuery.ToLower();
                query = query.Where(i => 
                    (i.Title != null && i.Title.ToLower().Contains(searchTerm)) ||
                    (i.Description != null && i.Description.ToLower().Contains(searchTerm)));
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(i => i.Category == request.Category);
            }

            if (!string.IsNullOrWhiteSpace(request.City))
            {
                query = query.Where(i => i.Location == request.City || i.User!.City == request.City);
            }

            if (request.MinPrice.HasValue)
            {
                query = query.Where(i => i.Price >= request.MinPrice.Value);
            }

            if (request.MaxPrice.HasValue)
            {
                query = query.Where(i => i.Price <= request.MaxPrice.Value);
            }

            if (request.MinRating.HasValue)
            {
                query = query.Where(i => i.AverageRating >= request.MinRating.Value);
            }

            // Apply sorting
            query = request.SortBy?.ToLower() switch
            {
                "price_asc" => query.OrderBy(i => i.Price),
                "price_desc" => query.OrderByDescending(i => i.Price),
                "rating" => query.OrderByDescending(i => i.AverageRating),
                "newest" => query.OrderByDescending(i => i.CreatedAt),
                "recommended" => query.OrderByDescending(i => i.ItemScore),
                _ => query.OrderByDescending(i => i.ItemScore) // default sort is now by ranking score
            };

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 20;
            
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new ItemWithOwner(
                    i.Id,
                    i.Title ?? "",
                    i.Description,
                    i.Price ?? 0,
                    i.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault() != null
                        ? i.Images.OrderBy(img => img.DisplayOrder).First().ImageUrl
                        : i.ImageUrl,
                    i.Location ?? i.User!.City,
                    i.Category,
                    i.IsListed,
                    i.IsAdminHidden,
                    i.Reviews.Any() ? i.Reviews.Average(r => r.Rating) : 0,
                    i.Reviews.Count,
                    i.CreatedAt,
                    i.UpdatedAt,
                    new UserSummary(
                        i.User!.Id,
                        i.User.UserName ?? "",
                        i.User.Email,
                        i.User.FirstName,
                        i.User.LastName,
                        i.User.City,
                        i.User.ProfilePictureUrl
                    )
                ))
                .ToListAsync();

            return Ok(new PaginatedResponse<ItemWithOwner>(
                items,
                totalCount,
                page,
                pageSize
            ));
        }
    }
}

