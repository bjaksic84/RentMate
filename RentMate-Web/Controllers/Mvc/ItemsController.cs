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
        private const int MaxImagesPerItem = 10;
        private const string AdminRole = "Admin";

        private readonly RentMateContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<RentMateHub> _hubContext;
        private readonly IFileUploadService _fileUploadService;
        private readonly IStringLocalizer<ItemsController> _localizer;
        private readonly IAccessoryService _accessoryService;
        private readonly IScoringService _scoringService;
        private readonly ILogger<ItemsController> _logger;

        public ItemsController(
            RentMateContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<RentMateHub> hubContext,
            IFileUploadService fileUploadService,
            IStringLocalizer<ItemsController> localizer,
            IAccessoryService accessoryService,
            IScoringService scoringService,
            ILogger<ItemsController> logger)
        {
            _db = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _fileUploadService = fileUploadService;
            _localizer = localizer;
            _accessoryService = accessoryService;
            _scoringService = scoringService;
            _logger = logger;
        }

        [Authorize(Roles = AdminRole)]
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

            var item = await _db.Items
                .Include(i => i.User)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder).Take(MaxImagesPerItem))
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
            if (currentUserId != null && !string.IsNullOrEmpty(item.Category))
            {
                await _scoringService.RecordCategoryInteractionAsync(currentUserId, item.Category);
            }

            // Owner aggregate stats
            var ownerCompletedRentals = await _db.Rentals
                .Where(r => r.OwnerId == item.UserId && r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Completed)
                .CountAsync();

            var ownerAverageRating = await _db.Reviews
                .Where(r => r.Item != null && r.Item.UserId == item.UserId && !r.IsDeleted)
                .Select(r => (double?)r.Rating)
                .AverageAsync() ?? 0;

            // Star distribution (in memory from already-loaded reviews)
            var starCounts = new int[5];
            foreach (var group in item.Reviews.GroupBy(r => r.Rating))
            {
                var index = group.Key - 1; // Rating 1 → index 0, Rating 5 → index 4
                if (index >= 0 && index < 5)
                    starCounts[index] = group.Count();
            }

            // Can the current user leave a review?
            var isSignedIn = currentUserId != null;
            var isOwner = currentUserId != null && item.UserId == currentUserId;
            var canReview = false;

            if (isSignedIn && !isOwner)
            {
                var hasCompletedRental = await _db.Rentals.AnyAsync(r =>
                    r.ItemId == item.Id
                    && r.RenterId == currentUserId
                    && r.Status == RentMate.Shared.Contracts.Responses.RentalStatus.Completed);

                var hasExistingReview = await _db.Reviews.AnyAsync(r =>
                    r.ItemId == item.Id
                    && r.ReviewerId == currentUserId
                    && !r.IsDeleted);

                canReview = hasCompletedRental && !hasExistingReview;
            }

            // Similar items
            var similarItems = await GetSimilarItemsAsync(item.Id, item.Category, item.User?.City, item.Price ?? 0);

            // Map setup
            var cityCoordinates = CityData.GetCoordinates(item.User?.City);

            var ownerName = item.User != null
                ? $"{item.User.FirstName} {item.User.LastName}".Trim()
                : string.Empty;

            var viewModel = new ItemDetailsViewModel
            {
                // Core item data
                ItemId = item.Id,
                Title = item.Title ?? string.Empty,
                Description = item.Description,
                Price = item.Price ?? 0,
                Category = item.Category,
                DepositAmount = item.DepositAmount,
                MaxRentalDays = item.MaxRentalDays,
                AutoApproveExtensions = item.AutoApproveExtensions,
                IsListed = item.IsListed,

                // Images
                Images = item.Images.Select(img => new ItemImageViewModel
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    DisplayOrder = img.DisplayOrder
                }).ToList(),
                PrimaryImageUrl = item.PrimaryImageUrl,

                // Owner profile
                OwnerId = item.UserId ?? string.Empty,
                OwnerName = string.IsNullOrWhiteSpace(ownerName) ? (item.User?.UserName ?? string.Empty) : ownerName,
                OwnerCity = item.User?.City,
                OwnerProfilePictureUrl = item.User?.ProfilePictureUrl,
                OwnerMemberSince = item.User?.CreatedAt ?? DateTime.UtcNow,
                OwnerIsPhoneVerified = item.User?.IsPhoneVerified ?? false,
                OwnerIsGovernmentIdVerified = item.User?.IsGovernmentIdVerified ?? false,
                OwnerResponseRate = item.User?.ResponseRate ?? 0,
                OwnerAvgResponseTimeHours = item.User?.AvgResponseTimeHours ?? 0,
                OwnerCompletedRentals = ownerCompletedRentals,
                OwnerAverageRating = ownerAverageRating,
                OwnerTrustScore = item.User?.ProfileTrustScore ?? 0,

                // Reviews
                AverageRating = item.AverageRating,
                ReviewCount = item.ReviewCount,
                StarCounts = starCounts,
                Reviews = item.Reviews.OrderByDescending(r => r.CreatedAt).Select(r => new ReviewViewModel
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Title = r.Title,
                    Body = r.Body,
                    IsAnonymous = r.IsAnonymous,
                    ReviewerId = r.ReviewerId,
                    ReviewerName = r.IsAnonymous ? null : (r.Reviewer != null
                        ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}".Trim()
                        : null),
                    ReviewerProfilePictureUrl = r.IsAnonymous ? null : r.Reviewer?.ProfilePictureUrl,
                    CreatedAt = r.CreatedAt
                }).ToList(),
                CanReview = canReview,
                IsSignedIn = isSignedIn,

                // Accessories
                Accessories = item.Accessories.Select(a => new AccessoryViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    DailyPrice = a.DailyPrice
                }).ToList(),

                // Blocked date ranges
                BlockedDateRanges = item.Rentals.Select(r => new RentalDateRange
                {
                    StartDate = r.StartDate,
                    EndDate = r.EndDate
                }).ToList(),

                // Similar items
                SimilarItems = similarItems,

                // Map
                MapLat = cityCoordinates.Lat,
                MapLng = cityCoordinates.Lng,
                MapCityName = cityCoordinates.Name,

                // User context
                CurrentUserId = currentUserId,
                IsFavorited = item.FavoritedBy.Any(),
                IsOwner = isOwner,

                // Modal support (raw entity)
                Item = item
            };

            return View(viewModel);
        }

        /// <summary>
        /// Finds up to 4 similar items in the same category, prioritizing same city and closest price.
        /// </summary>
        private async Task<List<SimilarItemViewModel>> GetSimilarItemsAsync(int itemId, string? category, string? city, decimal price)
        {
            if (string.IsNullOrEmpty(category)) return new();

            return await _db.Items
                .Where(i => i.IsListed && !i.IsAdminHidden && i.Id != itemId && i.Category == category)
                .OrderBy(i => i.User != null && i.User.City == city ? 0 : 1)
                .ThenBy(i => Math.Abs((i.Price ?? 0) - price))
                .Take(4)
                .Select(i => new SimilarItemViewModel
                {
                    Id = i.Id,
                    Title = i.Title ?? string.Empty,
                    Price = i.Price ?? 0,
                    PrimaryImageUrl = i.Images.OrderBy(img => img.DisplayOrder).Select(img => img.ImageUrl).FirstOrDefault() ?? i.ImageUrl,
                    AverageRating = i.AverageRating,
                    ReviewCount = i.ReviewCount,
                    City = i.User != null ? i.User.City : null,
                    Category = i.Category
                })
                .ToListAsync();
        }

        [Authorize]
        public async Task<IActionResult> Create()
        {
            // Government ID gate: must verify ID before listing items
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !user.IsGovernmentIdVerified)
            {
                TempData["ErrorMessage"] = "You must verify your government ID before listing items for rent. Please go to your profile settings to complete verification.";
                return RedirectToAction("UserDashboard", "Dashboard");
            }

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

                // Validate image count before saving anything
                if (images != null && images.Count > MaxImagesPerItem)
                {
                    TempData["ErrorMessage"] = string.Format(_localizer["Maximum {0} images allowed"], MaxImagesPerItem);
                                        return View(item);
                }

                _db.Add(item);
                await _db.SaveChangesAsync();

                // Handle multiple image uploads
                if (images != null && images.Count > 0)
                {
                    var uploadResult = await _fileUploadService.UploadFilesAsync(images, "items");
                    if (!uploadResult.AllSucceeded)
                    {
                        TempData["ErrorMessage"] = _localizer["Some images failed to upload. Please try again."].Value;
                    }
                    else
                    {
                        for (int i = 0; i < uploadResult.SuccessfulUrls.Count; i++)
                        {
                            var itemImage = new ItemImage
                            {
                                ItemId = item.Id,
                                ImageUrl = uploadResult.SuccessfulUrls[i],
                                DisplayOrder = i,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.ItemImages.Add(itemImage);
                        }
                        await _db.SaveChangesAsync();
                    }
                }

                // Event-driven: compute initial ItemScore for the new listing
                _ = Task.Run(() => _scoringService.ComputeAndSaveItemScoreAsync(item.Id));

                TempData["SuccessMessage"] = string.Format(_localizer["Item '{0}' created successfully!"], item.Title);
                return RedirectToAction("UserDashboard", "Dashboard");
            }

                        return View(item);
        }

        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _db.Items
                .Include(i => i.Accessories)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder).Take(MaxImagesPerItem))
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null) return NotFound();

            // Check if user owns the item
            var userId = _userManager.GetUserId(User);
            if(item.UserId != userId) return Forbid();

                        return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Price,Category,IsListed,DepositAmount,AutoApproveExtensions,MaxRentalDays")] Item updatedItem, List<IFormFile>? images)
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
                    var existingCount = item.Images.Count;
                    if (existingCount + images.Count > MaxImagesPerItem)
                    {
                        TempData["ErrorMessage"] = string.Format(_localizer["Maximum {0} images allowed. You currently have {1}."], MaxImagesPerItem, existingCount);
                        return RedirectToAction(nameof(Edit), new { id });
                    }

                    var currentMaxOrder = item.Images.Any() ? item.Images.Max(i => i.DisplayOrder) : -1;
                    var uploadResult = await _fileUploadService.UploadFilesAsync(images, "items");

                    if (!uploadResult.AllSucceeded)
                    {
                        TempData["ErrorMessage"] = _localizer["Some images failed to upload. Please try again."].Value;
                    }
                    else
                    {
                        for (int i = 0; i < uploadResult.SuccessfulUrls.Count; i++)
                        {
                            var itemImage = new ItemImage
                            {
                                ItemId = item.Id,
                                ImageUrl = uploadResult.SuccessfulUrls[i],
                                DisplayOrder = currentMaxOrder + 1 + i,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.ItemImages.Add(itemImage);
                        }
                    }
                }

                await _db.SaveChangesAsync();

                // Event-driven: recompute ItemScore after edit (content/photos changed)
                await _scoringService.ComputeAndSaveItemScoreAsync(item.Id);

                return RedirectToAction("UserDashboard", "Dashboard");
            }
            return View(updatedItem);
        }

        [Authorize]
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

                // Delete all images from Cloudinary (including legacy single-image field)
                var imageUrls = item.Images.Select(i => i.ImageUrl).ToList();
                if (!string.IsNullOrEmpty(item.ImageUrl) && !imageUrls.Contains(item.ImageUrl))
                    imageUrls.Add(item.ImageUrl);
                await _fileUploadService.DeleteFilesAsync(imageUrls);

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

            // Prevent unlisting items with active rentals
            if (item.IsListed)
            {
                var hasActiveRentals = await _db.Rentals.AnyAsync(r =>
                    r.ItemId == id
                    && r.Status != Shared.Contracts.Responses.RentalStatus.Completed
                    && r.Status != Shared.Contracts.Responses.RentalStatus.Cancelled);
                if (hasActiveRentals)
                    return Json(new { success = false, message = "Cannot unlist item with active rentals." });
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
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var accessory = await _accessoryService.UpdateAccessoryAsync(accessoryId, userId, name, dailyPrice, isAvailable, description);
                return Json(new { success = true, accessory = new { accessory.Id, accessory.Name, accessory.DailyPrice, accessory.Description, accessory.IsAvailable } });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
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
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await _accessoryService.DeleteAccessoryAsync(accessoryId, userId);
                return Json(new { success = true });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AdminRole)]
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

            // Delete from Cloudinary first — proceed even if it fails (log warning)
            var cloudDeleted = await _fileUploadService.DeleteFileAsync(image.ImageUrl);
            if (!cloudDeleted)
            {
                _logger.LogWarning("Failed to delete image {ImageId} from Cloudinary (URL: {Url}). Proceeding with DB removal.", imageId, image.ImageUrl);
            }

            var itemId = image.ItemId;

            // Remove from database
            _db.ItemImages.Remove(image);
            await _db.SaveChangesAsync();

            // Reorder remaining images
            var remainingImages = await _db.ItemImages
                .Where(i => i.ItemId == itemId)
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

            // Get all images for this item and re-normalize order
            var allImages = await _db.ItemImages
                .Where(i => i.ItemId == image.ItemId)
                .OrderBy(i => i.DisplayOrder)
                .ToListAsync();

            // Put the selected image first, keep relative order of the rest
            var reordered = new List<ItemImage> { image };
            reordered.AddRange(allImages.Where(i => i.Id != imageId));

            for (int i = 0; i < reordered.Count; i++)
            {
                reordered[i].DisplayOrder = i;
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

            // Validate: all provided IDs must belong to this item
            var imageIdSet = images.Select(i => i.Id).ToHashSet();
            if (imageIds.Any(id => !imageIdSet.Contains(id)))
            {
                return BadRequest(new { success = false, message = "Invalid image IDs" });
            }

            // Validate: all item images must be in the provided list
            if (imageIds.Count != images.Count)
            {
                return BadRequest(new { success = false, message = "Image count mismatch" });
            }

            for (int i = 0; i < imageIds.Count; i++)
            {
                var image = images.First(img => img.Id == imageIds[i]);
                image.DisplayOrder = i;
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

            // Validate image count
            var existingCount = item.Images.Count;
            if (existingCount + images.Count > MaxImagesPerItem)
            {
                return BadRequest(new { success = false, message = string.Format(_localizer["Maximum {0} images allowed. You currently have {1}."].Value, MaxImagesPerItem, existingCount) });
            }

            var currentMaxOrder = item.Images.Any() ? item.Images.Max(i => i.DisplayOrder) : -1;
            var uploadResult = await _fileUploadService.UploadFilesAsync(images, "items");

            if (!uploadResult.AllSucceeded)
            {
                return BadRequest(new { success = false, message = _localizer["Some images failed to upload. Please try again."].Value });
            }

            for (int i = 0; i < uploadResult.SuccessfulUrls.Count; i++)
            {
                var itemImage = new ItemImage
                {
                    ItemId = item.Id,
                    ImageUrl = uploadResult.SuccessfulUrls[i],
                    DisplayOrder = currentMaxOrder + 1 + i,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ItemImages.Add(itemImage);
            }
            await _db.SaveChangesAsync(); // Single save for all images

            // Retrieve IDs after save
            var savedImages = await _db.ItemImages
                .Where(i => i.ItemId == item.Id && i.DisplayOrder > currentMaxOrder)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new { i.Id, i.ImageUrl, i.DisplayOrder })
                .ToListAsync();

            return Json(new { success = true, images = savedImages });
        }
    }
}