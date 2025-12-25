using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;
using System.Linq;
using System.Threading.Tasks;

namespace RentMate.Controllers
{
    public class ProfileController : Controller
    {
        private readonly RentMateContext _context;

        public ProfileController(RentMateContext context)
        {
            _context = context;
        }

        // GET: /Profile/Details/{userId}
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users
                .Include(u => u.Items.Where(i => i.IsListed)) // Show only active listings
                    .ThenInclude(i => i.Reviews) // For rating calculation
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            var allReviews = user.Items.SelectMany(i => i.Reviews).Where(r => !r.IsDeleted).ToList();
            ViewBag.ReviewCount = allReviews.Count;
            ViewBag.AverageRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 0;

            return View(user);
        }
    }
}