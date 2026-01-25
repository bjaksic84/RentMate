using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using RentMate.Shared.Contracts.Requests;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Controllers
{
    /// <summary>
    /// API controller for rental operations.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("ApiPolicy")]
    [Produces("application/json")]
    public class RentalsApiController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<RentMate.Models.ApplicationUser> _userManager;

        public RentalsApiController(RentMateContext context, UserManager<RentMate.Models.ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Create a new rental request.
        /// </summary>
        /// <param name="request">The rental request details.</param>
        /// <returns>The created rental summary.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(RentalSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RentalSummary>> PostRental(CreateRentalRequest request)
        {
            var renterId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(renterId)) return Unauthorized();

            var item = await _context.Items.Include(i => i.User).FirstOrDefaultAsync(i => i.Id == request.ItemId);
            if (item == null) return NotFound("Item not found.");

            if (item.UserId == renterId) return BadRequest("You cannot rent your own item.");

            // Calculate rental days and total price
            int days = (request.EndDate - request.StartDate).Days;
            if (days <= 0) days = 1;
            var totalPrice = (item.Price ?? 0m) * days;

            var dbRental = new RentMate.Models.Rental
            {
                ItemId = request.ItemId,
                RenterId = renterId,
                OwnerId = item.UserId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                RentalDate = DateTime.Now,
                Status = RentalStatus.Pending,
                TotalPrice = totalPrice
            };

            _context.Rentals.Add(dbRental);
            await _context.SaveChangesAsync();

            var renter = await _userManager.FindByIdAsync(renterId);
            
            return Ok(new RentalSummary(
                dbRental.Id,
                item.Title ?? "Untitled",
                item.Id,
                item.ImageUrl,
                renter?.UserName ?? "Unknown",
                item.User?.UserName ?? "Unknown",
                dbRental.StartDate,
                dbRental.EndDate,
                dbRental.TotalPrice,
                dbRental.Status,
                dbRental.RentalDate
            ));
        }

        /// <summary>
        /// Update a rental's status.
        /// </summary>
        /// <param name="id">The rental ID.</param>
        /// <param name="newStatus">The new status.</param>
        [HttpPatch("{id}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] RentalStatus newStatus)
        {
            var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == id);
            if (rental == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (rental.Item?.UserId != userId && rental.RenterId != userId) return Forbid();

            rental.Status = newStatus;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Get rentals where the current user is the renter.
        /// </summary>
        /// <returns>List of rental summaries with review status.</returns>
        [HttpGet("my-rentals")]
        [ProducesResponseType(typeof(IEnumerable<RentalDetails>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<RentalDetails>>> GetMyRentals()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var rentals = await _context.Rentals
                .Where(r => r.RenterId == userId)
                .Include(r => r.Item).ThenInclude(i => i!.User)
                .Include(r => r.Renter)
                .Include(r => r.Reviews.Where(rv => rv.ReviewerId == userId && !rv.IsDeleted))
                .OrderByDescending(r => r.RentalDate)
                .ToListAsync();

            var result = rentals.Select(r => new RentalDetails(
                r.Id,
                r.StartDate,
                r.EndDate,
                r.TotalPrice,
                r.Status,
                r.RentalDate,
                r.Item != null ? new ItemSummary(
                    r.Item.Id,
                    r.Item.Title ?? "Untitled",
                    r.Item.Description,
                    r.Item.Price ?? 0,
                    r.Item.ImageUrl,
                    r.Item.Location,
                    r.Item.IsListed,
                    r.Item.IsAdminHidden,
                    r.Item.AverageRating ?? 0,
                    r.Item.ReviewCount,
                    r.Item.CreatedAt
                ) : null!,
                r.Renter != null ? new UserSummary(
                    r.Renter.Id,
                    r.Renter.UserName ?? "",
                    r.Renter.Email,
                    r.Renter.FirstName,
                    r.Renter.LastName,
                    r.Renter.City,
                    r.Renter.ProfilePictureUrl
                ) : null!,
                r.Item?.User != null ? new UserSummary(
                    r.Item.User.Id,
                    r.Item.User.UserName ?? "",
                    r.Item.User.Email,
                    r.Item.User.FirstName,
                    r.Item.User.LastName,
                    r.Item.User.City,
                    r.Item.User.ProfilePictureUrl
                ) : null!,
                r.Reviews.FirstOrDefault() is { } review ? new ReviewSummary(
                    review.Id,
                    review.Rating,
                    review.Body,
                    review.CreatedAt,
                    r.Renter?.UserName ?? "",
                    r.Renter?.ProfilePictureUrl
                ) : null
            )).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Get rentals where the current user is the item owner.
        /// </summary>
        /// <returns>List of rental summaries.</returns>
        [HttpGet("owner-rentals")]
        [ProducesResponseType(typeof(IEnumerable<RentalDetails>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<RentalDetails>>> GetOwnerRentals()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var rentals = await _context.Rentals
                .Where(r => r.OwnerId == userId)
                .Include(r => r.Item).ThenInclude(i => i!.User)
                .Include(r => r.Renter)
                .OrderByDescending(r => r.RentalDate)
                .ToListAsync();

            var result = rentals.Select(r => new RentalDetails(
                r.Id,
                r.StartDate,
                r.EndDate,
                r.TotalPrice,
                r.Status,
                r.RentalDate,
                r.Item != null ? new ItemSummary(
                    r.Item.Id,
                    r.Item.Title ?? "Untitled",
                    r.Item.Description,
                    r.Item.Price ?? 0,
                    r.Item.ImageUrl,
                    r.Item.Location,
                    r.Item.IsListed,
                    r.Item.IsAdminHidden,
                    r.Item.AverageRating ?? 0,
                    r.Item.ReviewCount,
                    r.Item.CreatedAt
                ) : null!,
                r.Renter != null ? new UserSummary(
                    r.Renter.Id,
                    r.Renter.UserName ?? "",
                    r.Renter.Email,
                    r.Renter.FirstName,
                    r.Renter.LastName,
                    r.Renter.City,
                    r.Renter.ProfilePictureUrl
                ) : null!,
                r.Item?.User != null ? new UserSummary(
                    r.Item.User.Id,
                    r.Item.User.UserName ?? "",
                    r.Item.User.Email,
                    r.Item.User.FirstName,
                    r.Item.User.LastName,
                    r.Item.User.City,
                    r.Item.User.ProfilePictureUrl
                ) : null!,
                null // Reviews not included for owner view
            )).ToList();

            return Ok(result);
        }
    }
}