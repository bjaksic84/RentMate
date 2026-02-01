using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Services;
using System.Security.Claims;

namespace RentMate.Controllers
{
    /// <summary>
    /// Controller for viewing user profiles.
    /// </summary>
    public class ProfileController : Controller
    {
        private readonly RentMateContext _context;

        public ProfileController(RentMateContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Redirects to the current user's profile details.
        /// </summary>
        [HttpGet("/Profile")]
        public IActionResult Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(currentUserId))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return RedirectToAction(nameof(Details), new { id = currentUserId });
        }

        /// <summary>
        /// Displays the profile details for a specific user.
        /// </summary>
        /// <param name="id">The user's ID.</param>
        [HttpGet("/Profile/Details/{id}")]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Items!.Where(i => i.IsListed))
                    .ThenInclude(i => i.Reviews)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var (averageRating, reviewCount) = user.GetAllActiveReviews().CalculateRatingStats();
            
            ViewBag.ReviewCount = reviewCount;
            ViewBag.AverageRating = averageRating;

            return View(user);
        }
    }
}