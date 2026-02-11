using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Hubs;
using RentMate.Services.Interfaces;
using RentMate.Services.Extensions;
using RentMate.Services.Implementations;
using RentMate.Helpers;

namespace RentMate.Controllers.Mvc
{
    /// <summary>
    /// Controller for managing item CRUD operations and listings.
    /// </summary>
    public class ItemsController : Controller
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<RentMateHub> _hubContext;
        private readonly IFileUploadService _fileUploadService;
        private readonly IStringLocalizer<ItemsController> _localizer;
        private readonly IAccessoryService _accessoryService;

        private const string ItemsFolder = "items";
        private const string DashboardAction = "UserDashboard";
        private const string DashboardController = "Dashboard";

        public ItemsController(
            RentMateContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<RentMateHub> hubContext,
            IFileUploadService fileUploadService,
            IStringLocalizer<ItemsController> localizer,
            IAccessoryService accessoryService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _fileUploadService = fileUploadService;
            _localizer = localizer;
            _accessoryService = accessoryService;
        }

        // GET: Items
        public async Task<IActionResult> Index()
        {
            var rentMateContext = _context.Items.Include(i => i.User);
            return View(await rentMateContext.ToListAsync());
        }

        /// <summary>
        /// Displays detailed information about a specific item.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("Items/Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            var item = await LoadItemWithDetailsAsync(id.Value, currentUserId);
            if (item == null)
            {
                return NotFound();
            }

            SetMapViewData(item.User?.City, currentUserId);

            return View(item);
        }

        private async Task<Item?> LoadItemWithDetailsAsync(int itemId, string? currentUserId)
        {
            return await _context.Items
                .Include(i => i.User)
                .Include(i => i.Reviews.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Reviewer)
                .Include(i => i.FavoritedBy.Where(f => f.AccountId == currentUserId))
                .Include(i => i.Accessories.Where(a => a.IsAvailable))
                .Include(i => i.Rentals.Where(r => r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Active
                    || r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Pending))
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == itemId);
        }

        private void SetMapViewData(string? ownerCity, string? currentUserId)
        {
            var cityCoordinates = CityData.GetCoordinates(ownerCity);
            ViewBag.MapLat = cityCoordinates.Lat;
            ViewBag.MapLng = cityCoordinates.Lng;
            ViewBag.MapCityName = cityCoordinates.Name;
            ViewBag.CurrentUserId = currentUserId;
        }

        // GET: Items/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email");
            return View();
        }

        /// <summary>
        /// Creates a new item listing.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("Title,Description,Price,Category,Location,DepositAmount,AutoApproveExtensions,MaxRentalDays")] Item item, IFormFile? image)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                // Stay on the Create page and show validation errors
                ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email");
                return View(item);
            }

            await InitializeNewItemAsync(item, currentUser.Id, image);

            _context.Add(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = string.Format(_localizer["Item '{0}' created successfully!"], item.Title);
            return RedirectToAction(DashboardAction, DashboardController);
        }

        private async Task InitializeNewItemAsync(Item item, string ownerId, IFormFile? image)
        {
            if (image != null)
            {
                item.ImageUrl = await _fileUploadService.UploadFileAsync(image, ItemsFolder);
            }

            item.UserId = ownerId;
            item.IsListed = false;
            item.IsRented = false;
            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
        }

        // GET: Items/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .Include(i => i.Accessories)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (item == null) return NotFound();

            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", item.UserId);
            return View(item);
        }

        /// <summary>
        /// Updates an existing item.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Price,Category,IsListed,ImageUrl,DepositAmount,AutoApproveExtensions,MaxRentalDays")] Item updatedItem, IFormFile? image)
        {
            if (id != updatedItem.Id)
            {
                return NotFound();
            }

            var existingItem = await _context.Items.FindAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (!IsAuthorizedToModify(currentUser, existingItem))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(updatedItem);
            }

            await UpdateItemPropertiesAsync(existingItem, updatedItem, image);
            await _context.SaveChangesAsync();

            return RedirectToAction(DashboardAction, DashboardController);
        }

        private bool IsAuthorizedToModify(ApplicationUser? user, Item item)
        {
            return user != null && item.UserId == user.Id;
        }

        private async Task UpdateItemPropertiesAsync(Item existingItem, Item updatedItem, IFormFile? newImage)
        {
            existingItem.Title = updatedItem.Title;
            existingItem.Description = updatedItem.Description;
            existingItem.Price = updatedItem.Price;
            existingItem.Category = updatedItem.Category;
            existingItem.DepositAmount = updatedItem.DepositAmount;
            existingItem.AutoApproveExtensions = updatedItem.AutoApproveExtensions;
            existingItem.MaxRentalDays = updatedItem.MaxRentalDays;
            existingItem.UpdatedAt = DateTime.UtcNow;

            if (newImage != null)
            {
                await ReplaceItemImageAsync(existingItem, newImage);
            }
        }

        private async Task ReplaceItemImageAsync(Item item, IFormFile newImage)
        {
            if (!string.IsNullOrEmpty(item.ImageUrl))
            {
                _fileUploadService.DeleteFile(item.ImageUrl);
            }
            item.ImageUrl = await _fileUploadService.UploadFileAsync(newImage, ItemsFolder);
        }

        // GET: Items/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .Include(i => i.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!IsAuthorizedToModify(currentUser, item)) return Forbid();

            ViewBag.HasActiveRentals = await HasActiveRentalsAsync(id.Value);

            return View(item);
        }

        /// <summary>
        /// Deletes an item after confirmation.
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Items.FindAsync(id);
            var currentUser = await _userManager.GetUserAsync(User);

            if (item != null && IsAuthorizedToModify(currentUser, item))
            {
                if (await HasActiveRentalsAsync(id))
                {
                    TempData["ErrorMessage"] = _localizer["Cannot delete item with active rentals. Please wait until all rentals are completed or cancelled."].Value;
                    return RedirectToAction(nameof(Delete), new { id });
                }

                DeleteItemImage(item);
                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(DashboardAction, DashboardController);
        }

        private async Task<bool> HasActiveRentalsAsync(int itemId)
        {
            return await _context.Rentals.AnyAsync(r =>
                r.ItemId == itemId &&
                r.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Completed &&
                r.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Cancelled);
        }

        private void DeleteItemImage(Item item)
        {
            if (!string.IsNullOrEmpty(item.ImageUrl))
            {
                _fileUploadService.DeleteFile(item.ImageUrl);
            }
        }

        /// <summary>
        /// Toggles the listing status of an item.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleListing(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var item = await _context.Items.FindAsync(id);

            if (!IsAuthorizedToModify(currentUser, item!))
            {
                return Unauthorized();
            }

            item!.IsListed = !item.IsListed;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await BroadcastListingChangeAsync(item);

            return Json(new { success = true, isListed = item.IsListed });
        }

        private async Task BroadcastListingChangeAsync(Item item)
        {
            await _hubContext.Clients.All.SendAsync("ItemListingChanged", new
            {
                itemId = item.Id,
                isListed = item.IsListed,
                title = item.Title,
                price = item.Price,
                description = item.Description
            });
        }

        /// <summary>
        /// Loads the reviews partial view for an item.
        /// </summary>
        [HttpGet("LoadReviewsPartial/{itemId}")]
        public async Task<IActionResult> LoadReviewsPartial(int itemId)
        {
            var item = await _context.Items
                .Include(i => i.Reviews.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Reviewer)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null)
            {
                return NotFound();
            }

            var sortedReviews = item.Reviews
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return PartialView("~/Views/Shared/_ReviewsPartial.cshtml", sortedReviews);
        }

        #region Booked Dates

        /// <summary>
        /// Returns booked date ranges for an item (Active/Pending rentals).
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetBookedDates(int itemId)
        {
            var rentals = await _context.Rentals
                .AsNoTracking()
                .Where(r => r.ItemId == itemId
                    && (r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Active
                        || r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Pending))
                .Select(r => new { from = r.StartDate.ToString("yyyy-MM-dd"), to = r.EndDate.ToString("yyyy-MM-dd"), r.Id })
                .ToListAsync();

            return Json(rentals);
        }

        #endregion

        #region Accessory Management

        /// <summary>
        /// Gets accessories for an item as JSON.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAccessories(int itemId)
        {
            var item = await _context.Items.FindAsync(itemId);
            if (item == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!IsAuthorizedToModify(currentUser, item)) return Forbid();

            var accessories = await _accessoryService.GetAccessoriesForItemAsync(itemId);
            return Json(accessories.Select(a => new
            {
                a.Id,
                a.Name,
                a.DailyPrice,
                a.Description,
                a.IsAvailable
            }));
        }

        /// <summary>
        /// Adds a new accessory to an item.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAccessory(int itemId, string name, decimal dailyPrice, string? description)
        {
            var item = await _context.Items.FindAsync(itemId);
            if (item == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!IsAuthorizedToModify(currentUser, item)) return Forbid();

            var accessory = await _accessoryService.AddAccessoryAsync(itemId, name, dailyPrice, description);
            return Json(new { success = true, accessory = new { accessory.Id, accessory.Name, accessory.DailyPrice, accessory.Description, accessory.IsAvailable } });
        }

        /// <summary>
        /// Updates an existing accessory.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAccessory(int accessoryId, string name, decimal dailyPrice, bool isAvailable, string? description)
        {
            try
            {
                var accessory = await _accessoryService.UpdateAccessoryAsync(accessoryId, name, dailyPrice, isAvailable, description);
                return Json(new { success = true, accessory = new { accessory.Id, accessory.Name, accessory.DailyPrice, accessory.Description, accessory.IsAvailable } });
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Deletes an accessory.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccessory(int accessoryId)
        {
            try
            {
                await _accessoryService.DeleteAccessoryAsync(accessoryId);
                return Json(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        #endregion

        /// <summary>
        /// Toggles the admin-hidden status of an item. Admin only.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminToggleHide(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            item.IsAdminHidden = !item.IsAdminHidden;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isAdminHidden = item.IsAdminHidden });
        }
    }
}

