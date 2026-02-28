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
    public class ItemsController : Controller
    {
        private readonly RentMateContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<RentMateHub> _hubContext;
        private readonly IFileUploadService _fileUploadService;
        private readonly IStringLocalizer<ItemsController> _localizer;
        private readonly IAccessoryService _accessoryService;
        private readonly IScoringService _scoringService;

        public ItemsController(
            RentMateContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<RentMateHub> hubContext,
            IFileUploadService fileUploadService,
            IStringLocalizer<ItemsController> localizer,
            IAccessoryService accessoryService,
            IScoringService scoringService)
        {
            _db = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _fileUploadService = fileUploadService;
            _localizer = localizer;
            _accessoryService = accessoryService;
            _scoringService = scoringService;
        }

        public async Task<IActionResult> Index()
        {
            var items = _db.Items.Include(i => i.User);
            return View(await items.ToListAsync());
        }

        [AllowAnonymous]
        [HttpGet("Items/Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // TODO: Move this query to a repository later
            var item = await _db.Items
                .Include(i => i.User)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder))
                .Include(i => i.Reviews.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Reviewer)
                .Include(i => i.FavoritedBy.Where(f => f.AccountId == currentUserId))
                .Include(i => i.Accessories.Where(a => a.IsAvailable))
                .Include(i => i.Rentals.Where(r => r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Active
                    || r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Pending))
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();

            // Record page-view for ranking system
            await _scoringService.RecordItemViewAsync(item.Id);

            // Track category interaction for personalization
            var currentUserId2 = _userManager.GetUserId(User);
            if (currentUserId2 != null && !string.IsNullOrEmpty(item.Category))
            {
                _ = Task.Run(() => _scoringService.RecordCategoryInteractionAsync(currentUserId2, item.Category));
            }

            // Map setup
            var cityCoordinates = CityData.GetCoordinates(item.User?.City);
            ViewBag.MapLat = cityCoordinates.Lat;
            ViewBag.MapLng = cityCoordinates.Lng;
            ViewBag.MapCityName = cityCoordinates.Name;
            ViewBag.CurrentUserId = currentUserId;

            return View(item);
        }

        public async Task<IActionResult> Create()
        {
            // Government ID gate: must verify ID before listing items
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !user.IsGovernmentIdVerified)
            {
                TempData["ErrorMessage"] = "You must verify your government ID before listing items for rent. Please go to your profile settings to complete verification.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            ViewData["UserId"] = new SelectList(_db.Users, "Id", "Email");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("Title,Description,Price,Category,Location,DepositAmount,AutoApproveExtensions,MaxRentalDays")] Item item, List<IFormFile>? images)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (ModelState.IsValid)
            {
                item.UserId = user.Id;
                item.IsListed = false;
                item.IsRented = false;
                item.CreatedAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;

                _db.Add(item);
                await _db.SaveChangesAsync();

                // Handle multiple image uploads
                if (images != null && images.Count > 0)
                {
                    var uploadedUrls = await _fileUploadService.UploadFilesAsync(images, "items");
                    for (int i = 0; i < uploadedUrls.Count; i++)
                    {
                        var itemImage = new ItemImage
                        {
                            ItemId = item.Id,
                            ImageUrl = uploadedUrls[i],
                            DisplayOrder = i,
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.ItemImages.Add(itemImage);
                    }
                    await _db.SaveChangesAsync();
                }

                // Event-driven: compute initial ItemScore for the new listing
                _ = Task.Run(() => _scoringService.ComputeAndSaveItemScoreAsync(item.Id));

                TempData["SuccessMessage"] = string.Format(_localizer["Item '{0}' created successfully!"], item.Title);
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            ViewData["UserId"] = new SelectList(_db.Users, "Id", "Email");
            return View(item);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _db.Items
                .Include(i => i.Accessories)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder))
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null) return NotFound();

            // Check if user owns the item
            var userId = _userManager.GetUserId(User);
            if(item.UserId != userId) return Forbid();

            ViewData["UserId"] = new SelectList(_db.Users, "Id", "Email", item.UserId);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Price,Category,IsListed,ImageUrl,DepositAmount,AutoApproveExtensions,MaxRentalDays")] Item updatedItem, List<IFormFile>? images)
        {
            if (id != updatedItem.Id) return NotFound();

            var item = await _db.Items
                .Include(i => i.Images)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (item == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || item.UserId != user.Id)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                item.Title = updatedItem.Title;
                item.Description = updatedItem.Description;
                item.Price = updatedItem.Price;
                item.Category = updatedItem.Category;
                item.DepositAmount = updatedItem.DepositAmount;
                item.AutoApproveExtensions = updatedItem.AutoApproveExtensions;
                item.MaxRentalDays = updatedItem.MaxRentalDays;
                item.UpdatedAt = DateTime.UtcNow;

                // Handle new image uploads (append to existing)
                if (images != null && images.Count > 0)
                {
                    var currentMaxOrder = item.Images.Any() ? item.Images.Max(i => i.DisplayOrder) : -1;
                    var uploadedUrls = await _fileUploadService.UploadFilesAsync(images, "items");

                    for (int i = 0; i < uploadedUrls.Count; i++)
                    {
                        var itemImage = new ItemImage
                        {
                            ItemId = item.Id,
                            ImageUrl = uploadedUrls[i],
                            DisplayOrder = currentMaxOrder + 1 + i,
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.ItemImages.Add(itemImage);
                    }
                }

                await _db.SaveChangesAsync();

                // Event-driven: recompute ItemScore after edit (content/photos changed)
                await _scoringService.ComputeAndSaveItemScoreAsync(item.Id);

                return RedirectToAction("UserDashboard", "Dashboard");
            }
            return View(updatedItem);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _db.Items
                .Include(i => i.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || item.UserId != user.Id) return Forbid();

            // Check for active rentals
            ViewBag.HasActiveRentals = await _db.Rentals.AnyAsync(r =>
                r.ItemId == id &&
                r.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Completed &&
                r.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Cancelled);

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _db.Items
                .Include(i => i.Images)
                .FirstOrDefaultAsync(i => i.Id == id);
            var user = await _userManager.GetUserAsync(User);

            if (item != null && user != null && item.UserId == user.Id)
            {
                bool hasActiveRentals = await _db.Rentals.AnyAsync(r =>
                    r.ItemId == id &&
                    r.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Completed &&
                    r.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Cancelled);

                if (hasActiveRentals)
                {
                    TempData["ErrorMessage"] = _localizer["Cannot delete item with active rentals. Please wait until all rentals are completed or cancelled."].Value;
                    return RedirectToAction(nameof(Delete), new { id });
                }

                // Delete all images from Cloudinary
                var imageUrls = item.Images.Select(i => i.ImageUrl).ToList();
                if (!string.IsNullOrEmpty(item.ImageUrl))
                {
                    imageUrls.Add(item.ImageUrl); // Include legacy ImageUrl if exists
                }
                _fileUploadService.DeleteFiles(imageUrls);

                _db.Items.Remove(item);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("UserDashboard", "Dashboard");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleListing(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _db.Items.FindAsync(id);

            if (item == null || user == null || item.UserId != user.Id)
            {
                return Unauthorized();
            }

            // Government ID gate: prevent listing without verified ID
            if (!item.IsListed && !user.IsGovernmentIdVerified)
            {
                TempData["ErrorMessage"] = "You must verify your government ID before listing items for rent.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

            item.IsListed = !item.IsListed;
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify clients
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
            var item = await _db.Items
                .Include(i => i.Reviews.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Reviewer)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null) return NotFound();

            var sortedReviews = item.Reviews.OrderByDescending(r => r.CreatedAt).ToList();

            return PartialView("~/Views/Shared/_ReviewsPartial.cshtml", sortedReviews);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetBookedDates(int itemId)
        {
            var rentals = await _db.Rentals
                .AsNoTracking()
                .Where(r => r.ItemId == itemId
                    && (r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Active
                        || r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Pending))
                .Select(r => new { from = r.StartDate.ToString("yyyy-MM-dd"), to = r.EndDate.ToString("yyyy-MM-dd"), r.Id })
                .ToListAsync();

            return Json(rentals);
        }

        // Accessories logic
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAccessories(int itemId)
        {
            var item = await _db.Items.FindAsync(itemId);
            if (item == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (item.UserId != userId) return Forbid();

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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAccessory(int itemId, string name, decimal dailyPrice, string? description)
        {
            var item = await _db.Items.FindAsync(itemId);
            if (item == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (item.UserId != userId) return Forbid();

            var accessory = await _accessoryService.AddAccessoryAsync(itemId, name, dailyPrice, description);
            return Json(new { success = true, accessory = new { accessory.Id, accessory.Name, accessory.DailyPrice, accessory.Description, accessory.IsAvailable } });
        }

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
            catch (Exception)
            {
                return NotFound();
            }
        }

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
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminToggleHide(int id)
        {
            var item = await _db.Items.FindAsync(id);
            if (item == null) return NotFound();

            item.IsAdminHidden = !item.IsAdminHidden;
            await _db.SaveChangesAsync();

            return Json(new { success = true, isAdminHidden = item.IsAdminHidden });
        }

        // Image Management Endpoints

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetImages(int itemId)
        {
            var item = await _db.Items.FindAsync(itemId);
            if (item == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (item.UserId != userId) return Forbid();

            var images = await _db.ItemImages
                .Where(i => i.ItemId == itemId)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new { i.Id, i.ImageUrl, i.DisplayOrder })
                .ToListAsync();

            return Json(images);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _db.ItemImages
                .Include(i => i.Item)
                .FirstOrDefaultAsync(i => i.Id == imageId);

            if (image == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (image.Item?.UserId != userId) return Forbid();

            // Delete from Cloudinary
            _fileUploadService.DeleteFile(image.ImageUrl);

            // Remove from database
            _db.ItemImages.Remove(image);
            await _db.SaveChangesAsync();

            // Reorder remaining images
            var remainingImages = await _db.ItemImages
                .Where(i => i.ItemId == image.ItemId)
                .OrderBy(i => i.DisplayOrder)
                .ToListAsync();

            for (int i = 0; i < remainingImages.Count; i++)
            {
                remainingImages[i].DisplayOrder = i;
            }
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryImage(int imageId)
        {
            var image = await _db.ItemImages
                .Include(i => i.Item)
                .FirstOrDefaultAsync(i => i.Id == imageId);

            if (image == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (image.Item?.UserId != userId) return Forbid();

            // Get all images for this item and reorder
            var allImages = await _db.ItemImages
                .Where(i => i.ItemId == image.ItemId)
                .OrderBy(i => i.DisplayOrder)
                .ToListAsync();

            // Set the selected image to DisplayOrder 0, shift others
            var currentOrder = image.DisplayOrder;
            foreach (var img in allImages)
            {
                if (img.Id == imageId)
                {
                    img.DisplayOrder = 0;
                }
                else if (img.DisplayOrder < currentOrder)
                {
                    img.DisplayOrder += 1;
                }
            }
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderImages(int itemId, [FromBody] List<int> imageIds)
        {
            var item = await _db.Items.FindAsync(itemId);
            if (item == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (item.UserId != userId) return Forbid();

            var images = await _db.ItemImages
                .Where(i => i.ItemId == itemId)
                .ToListAsync();

            for (int i = 0; i < imageIds.Count; i++)
            {
                var image = images.FirstOrDefault(img => img.Id == imageIds[i]);
                if (image != null)
                {
                    image.DisplayOrder = i;
                }
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImages(int itemId, List<IFormFile> images)
        {
            var item = await _db.Items
                .Include(i => i.Images)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (item.UserId != userId) return Forbid();

            if (images == null || images.Count == 0)
            {
                return BadRequest(new { success = false, message = "No images provided" });
            }

            var currentMaxOrder = item.Images.Any() ? item.Images.Max(i => i.DisplayOrder) : -1;
            var uploadedUrls = await _fileUploadService.UploadFilesAsync(images, "items");
            var newImages = new List<object>();

            for (int i = 0; i < uploadedUrls.Count; i++)
            {
                var itemImage = new ItemImage
                {
                    ItemId = item.Id,
                    ImageUrl = uploadedUrls[i],
                    DisplayOrder = currentMaxOrder + 1 + i,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ItemImages.Add(itemImage);
                await _db.SaveChangesAsync(); // Save to get the ID

                newImages.Add(new { itemImage.Id, itemImage.ImageUrl, itemImage.DisplayOrder });
            }

            return Json(new { success = true, images = newImages });
        }
    }
}