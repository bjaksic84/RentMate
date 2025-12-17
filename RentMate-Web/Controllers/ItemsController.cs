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
using Microsoft.AspNetCore.SignalR;
using RentMate.Hubs;
using RentMate.Services;
using RentMate.Helpers;


namespace RentMate.Controllers
{
    public class ItemsController : Controller
    {
        private readonly RentMateContext _context;

        private readonly UserManager<ApplicationUser> _userManager;

        private readonly IHubContext<RentMateHub> _hubContext;

        private readonly IFileUploadService _fileService;
        public ItemsController(RentMateContext context, UserManager<ApplicationUser> userManager, IHubContext<RentMateHub> hubContext, IFileUploadService fileService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _fileService = fileService;
        }


        // GET: Items
        public async Task<IActionResult> Index()
        {
            var rentMateContext = _context.Items.Include(i => i.User);
            return View(await rentMateContext.ToListAsync());
        }

        // GET: Items/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .Include(i => i.User) // Lastnik
                .Include(i => i.Reviews.Where(r => !r.IsDeleted)) // Mnenja
                    .ThenInclude(r => r.Reviewer) // Kdo je napisal mnenje
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();
            // 1. Pridobi koordinate glede na mesto lastnika
            var cityInfo = CityData.GetCoordinates(item.User?.City);

            // 2. Pošlji podatke v View
            ViewBag.MapLat = cityInfo.Lat;
            ViewBag.MapLng = cityInfo.Lng;
            ViewBag.MapCityName = cityInfo.Name;

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
        public async Task<IActionResult> Create([Bind("Title,Description,Price,Category")] Item item, IFormFile? image)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (ModelState.IsValid)
            {
                // ✅ Assign ownership and safe defaults
                if (image != null)
                {
                    // Uporabimo mapo "items" na Cloudinary
                    item.ImageUrl = await _fileService.UploadFileAsync(image, "items");
                }
                item.UserId = user.Id;
                item.IsListed = false;    // start unlisted
                item.IsRented = false;    // not rented yet
                item.CreatedAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;

                _context.Add(item);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Item '{item.Title}' created successfully!";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            TempData["ErrorMessage"] = "Failed to create item. Please try again.";
            return RedirectToAction("UserDashboard", "Dashboard");
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Price,Category,IsListed,ImageUrl")] Item item, IFormFile? image)
        {
            if (id != item.Id) return NotFound();

            // Ker Bind ne vključuje UserId (varnost), ga moramo prebrati iz baze ali ohraniti
            // Najbolj varno je dobiti original iz baze in posodobiti polja
            var existingItem = await _context.Items.FindAsync(id);
            if (existingItem == null) return NotFound();
            
            var user = await _userManager.GetUserAsync(User);
            if (existingItem.UserId != user.Id) return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    // Posodobi polja
                    existingItem.Title = item.Title;
                    existingItem.Description = item.Description;
                    existingItem.Price = item.Price;
                    existingItem.Category = item.Category;
                    existingItem.UpdatedAt = DateTime.UtcNow;

                    // ✅ 2. Logika za zamenjavo slike
                    if (image != null)
                    {
                        // Izbriši staro sliko (če obstaja)
                        if (!string.IsNullOrEmpty(existingItem.ImageUrl))
                        {
                            _fileService.DeleteFile(existingItem.ImageUrl);
                        }
                        // Naloži novo
                        existingItem.ImageUrl = await _fileService.UploadFileAsync(image, "items");
                    }

                    _context.Update(existingItem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                throw;
                }
                return RedirectToAction("UserDashboard", "Dashboard");
            }
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
            var user = await _userManager.GetUserAsync(User);
            
            if (item != null && item.UserId == user.Id)
            {
                // ✅ 3. Izbriši sliko iz Cloudinary
                if (!string.IsNullOrEmpty(item.ImageUrl))
                {
                    _fileService.DeleteFile(item.ImageUrl);
                }

                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("UserDashboard", "Dashboard");
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
            // ✅ Broadcast real-time update
            await _hubContext.Clients.All.SendAsync("ItemListingChanged", new
            {
                itemId = item.Id,
                isListed = item.IsListed,
                title = item.Title,
                price = item.Price,
                description = item.Description
            });
            
            return Json(new { success = true, isListed = item.IsListed });
        }

        [HttpGet("LoadReviewsPartial/{itemId}")]
        public async Task<IActionResult> LoadReviewsPartial(int itemId)
        {
            var item = await _context.Items
                .Include(i => i.Reviews.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Reviewer)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null)
                return NotFound();

            // Sort newest first
            var reviews = item.Reviews.OrderByDescending(r => r.CreatedAt).ToList();

            return PartialView("~/Views/Shared/_ReviewsPartial.cshtml", reviews);
        }
    }
}
