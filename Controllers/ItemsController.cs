using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using Microsoft.AspNetCore.Authorization;
using RentMate.Models;
using Microsoft.AspNetCore.Identity;

namespace RentMate.Controllers
{
    public class ItemsController : Controller
    {
        private readonly RentMateContext _context;

        private readonly UserManager<ApplicationUser> _userManager;

        public ItemsController(RentMateContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // GET: Items
        public async Task<IActionResult> Index()
        {
            var rentMateContext = _context.Items.Include(i => i.User);
            return View(await rentMateContext.ToListAsync());
        }

        // GET: Items/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items
                .Include(i => i.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        // GET: Items/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email");
            return View();
        }

        // POST: Items/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        // POST: Items/Create

        public async Task<IActionResult> Create([Bind("Title,Description,Price,Category")] Item item)
        {
            var user = await _userManager.GetUserAsync(User); // 🧠 we fetch the logged-in user

            if (user == null)
                return Unauthorized();

            if (ModelState.IsValid)
            {
                item.UserId = user.Id;      // ✅ auto-assign ownership
                item.IsListed = false;      // 🧩 new items start as unlisted
                item.IsRented = false;      // ✅ default
                item.CreatedAt = DateTime.UtcNow; // optional metadata

                _context.Add(item);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Item '{item.Title}' created successfully!";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            return View(item);
        }



        // GET: Items/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", item.UserId);
            return View(item);
        }

        // POST: Items/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Price,UserId")] Item item)
        {
            if (id != item.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(item);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ItemExists(item.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", item.UserId);
            return View(item);
        }

        // GET: Items/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items
                .Include(i => i.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        // POST: Items/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item != null)
            {
                _context.Items.Remove(item);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ItemExists(int id)
        {
            return _context.Items.Any(e => e.Id == id);
        }
        // ItemsController.cs
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleListing(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.Items.FindAsync(id);
            if (item == null || item.UserId != user.Id) return Unauthorized();

            item.IsListed = !item.IsListed;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isListed = item.IsListed });
        }


    }
}
