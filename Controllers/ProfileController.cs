using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models;

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
                .Include(u => u.Items.Where(i => i.IsListed)) // Pokaži samo aktivne oglase
                    .ThenInclude(i => i.Reviews) // Za izračun ocene
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }
    }
}