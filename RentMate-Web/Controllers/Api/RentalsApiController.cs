using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Shared.Contracts.Requests;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Controllers.Api
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
        private readonly UserManager<ApplicationUser> _userManager;

        public RentalsApiController(RentMateContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Create a new rental request.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(RentalSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RentalSummary>> PostRental(CreateRentalRequest request)
        {
            var renterId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(renterId))
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Id == request.ItemId);

            if (item == null)
            {
                return NotFound("Item not found.");
            }

            if (item.UserId == renterId)
            {
                return BadRequest("You cannot rent your own item.");
            }

            var totalPrice = CalculateTotalPrice(item.Price, request.StartDate, request.EndDate);

            var newRental = new Rental
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

            _context.Rentals.Add(newRental);
            await _context.SaveChangesAsync();

            var renter = await _userManager.FindByIdAsync(renterId);

            return Ok(new RentalSummary(
                newRental.Id,
                item.Title ?? "Untitled",
                item.Id,
                item.ImageUrl,
                renter?.UserName ?? "Unknown",
                item.User?.UserName ?? "Unknown",
                newRental.StartDate,
                newRental.EndDate,
                newRental.TotalPrice,
                newRental.Status,
                newRental.RentalDate
            ));
        }

        private static decimal CalculateTotalPrice(decimal? pricePerDay, DateTime startDate, DateTime endDate)
        {
            var days = Math.Max((endDate - startDate).Days, 1);
            return (pricePerDay ?? 0m) * days;
        }

        /// <summary>
        /// Update a rental's status.
        /// </summary>
        [HttpPatch("{id}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] RentalStatus newStatus)
        {
            var rental = await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rental == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (rental.Item?.UserId != userId && rental.RenterId != userId)
            {
                return Forbid();
            }

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
                    r.Item.Category,
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
                    r.Item.Category,
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

