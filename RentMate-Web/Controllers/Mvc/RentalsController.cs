using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
using RentMate.Shared.Contracts.Responses;
using NotificationType = RentMate.Models.Domain.NotificationType;

namespace RentMate.Controllers.Mvc
{
    /// <summary>
    /// Controller for browsing items and managing rental requests.
    /// </summary>
    [Authorize]
    public class RentalsController : Controller
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<RentMateHub> _hubContext;
        private readonly IStringLocalizer<RentalsController> _localizer;
        private readonly ICurrencyService _currencyService;
        private readonly IAccessoryService _accessoryService;
        private readonly IDepositService _depositService;
        private readonly ILogger<RentalsController> _logger;
        private readonly IScoringService _scoringService;
        private readonly INotificationService _notificationService;

        private const int DefaultPageSize = 12;
        private const string DashboardAction = "UserDashboard";
        private const string DashboardController = "Dashboard";

        public RentalsController(
            RentMateContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<RentMateHub> hubContext,
            IStringLocalizer<RentalsController> localizer,
            ICurrencyService currencyService,
            IAccessoryService accessoryService,
            IDepositService depositService,
            ILogger<RentalsController> logger,
            IScoringService scoringService,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _localizer = localizer;
            _currencyService = currencyService;
            _accessoryService = accessoryService;
            _depositService = depositService;
            _logger = logger;
            _scoringService = scoringService;
            _notificationService = notificationService;
        }

        #region Helper Methods

        private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private IActionResult HandleError(string message)
        {
            if (IsAjaxRequest())
            {
                return BadRequest(message);
            }
            TempData["ErrorMessage"] = message;
            return RedirectToAction(DashboardAction, DashboardController);
        }

        private IActionResult HandleSuccess(string message, object? jsonData = null)
        {
            if (IsAjaxRequest())
            {
                return Json(jsonData ?? new { success = true, message });
            }
            TempData["SuccessMessage"] = message;
            return RedirectToAction(DashboardAction, DashboardController);
        }

        private bool HasDateConflict(ICollection<Rental>? rentals, DateTime startDate, DateTime endDate)
        {
            return rentals?.Any(r =>
                (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending || r.Status == RentalStatus.Accepted) &&
                r.StartDate <= endDate &&
                r.EndDate >= startDate) ?? false;
        }

        private static decimal CalculateTotalPrice(decimal? pricePerDay, DateTime startDate, DateTime endDate)
        {
            var rentalDays = Math.Max((endDate.Date - startDate.Date).Days + 1, 1);
            return (pricePerDay ?? 0) * rentalDays;
        }

        private bool IsUserAuthorizedForRental(ApplicationUser? user, Rental rental)
        {
            return user != null && (rental.OwnerId == user.Id || rental.RenterId == user.Id);
        }

        private async Task NotifyRentalStatusChangeAsync(Rental rental, string message)
        {
            await _hubContext.Clients.User(rental.RenterId!).SendAsync("RentalStatusChanged", new
            {
                rentalId = rental.Id,
                newStatus = rental.Status.ToString(),
                itemTitle = rental.Item?.Title,
                message
            });

            var notifType = rental.Status switch
            {
                RentalStatus.Accepted => NotificationType.RentalAccepted,
                RentalStatus.Completed => NotificationType.RentalCompleted,
                RentalStatus.Cancelled => NotificationType.RentalCancelled,
                _ => NotificationType.RentalApproved
            };
            var titleKey = "Notification." + notifType.ToString();
            var msgKey = "NotificationMsg." + notifType.ToString();
            await _notificationService.CreateAsync(
                rental.RenterId!, notifType,
                _localizer[titleKey].Value,
                string.Format(_localizer[msgKey].Value, rental.Item?.Title ?? ""),
                rental.Id, "Rental", "/Dashboard?tab=renting");

            // Auto-dismiss the original request notification for the owner
            if (rental.Status != RentalStatus.Pending)
                await _notificationService.AutoDismissAsync(rental.Id, "Rental", NotificationType.RentalRequested);
        }

        #endregion

        /// <summary>
        /// Displays available items for rent with filtering and pagination.
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? search,
            string[]? category,
            decimal? minPrice,
            decimal? maxPrice,
            string? city,
            DateTime? startDate,
            DateTime? endDate,
            string? sort,
            int? minRating,
            int page = 1)
        {
            var currentUserId = _userManager.GetUserId(User);
            var query = BuildBaseItemQuery(currentUserId);

            query = ApplySearchFilters(query, search, category, minPrice, maxPrice, city, minRating);
            query = ApplyAvailabilityFilter(query, startDate, endDate);
            query = ApplySorting(query, sort);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * DefaultPageSize)
                .Take(DefaultPageSize)
                .ToListAsync();

            await SetViewBagDataAsync(search, category, minPrice, maxPrice, city, startDate, endDate, sort, minRating, page, totalItems, currentUserId);

            return View(items);
        }

        private IQueryable<Item> BuildBaseItemQuery(string? currentUserId)
        {
            return _context.Items
                .AsNoTracking()
                .Include(i => i.User)
                .Include(i => i.Images.OrderBy(img => img.DisplayOrder).Take(1))
                .Include(i => i.Rentals.Where(r =>
                    (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending || r.Status == RentalStatus.Accepted) &&
                    r.EndDate >= DateTime.Today))
                .Include(i => i.FavoritedBy.Where(f => f.AccountId == currentUserId))
                .AsSplitQuery()
                .Where(i => i.IsListed && !i.IsAdminHidden);
        }

        private IQueryable<Item> ApplySearchFilters(
            IQueryable<Item> query,
            string? search,
            string[]? categories,
            decimal? minPrice,
            decimal? maxPrice,
            string? city,
            int? minRating)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(i =>
                    (i.Title != null && i.Title.ToLower().Contains(searchLower)) ||
                    (i.Description != null && i.Description.ToLower().Contains(searchLower)));
            }

            if (categories?.Length > 0)
            {
                query = query.Where(i => categories.Contains(i.Category));
            }

            if (minPrice.HasValue)
            {
                var minBasePrice = _currencyService.ConvertToBase(minPrice.Value);
                query = query.Where(i => i.Price >= minBasePrice);
            }

            if (maxPrice.HasValue)
            {
                var maxBasePrice = _currencyService.ConvertToBase(maxPrice.Value);
                query = query.Where(i => i.Price <= maxBasePrice);
            }

            if (!string.IsNullOrEmpty(city))
            {
                query = query.Where(i => i.User!.City == city);
            }

            if (minRating.HasValue && minRating.Value > 0)
            {
                query = query.Where(i => i.AverageRating.HasValue && i.AverageRating >= minRating.Value);
            }

            return query;
        }

        private static IQueryable<Item> ApplyAvailabilityFilter(IQueryable<Item> query, DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(i =>
                    !i.Rentals!.Any(r =>
                        (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending || r.Status == RentalStatus.Accepted) &&
                        r.StartDate <= endDate && r.EndDate >= startDate));
            }
            return query;
        }

        private static IQueryable<Item> ApplySorting(IQueryable<Item> query, string? sortBy)
        {
            return sortBy switch
            {
                "priceAsc" => query.OrderBy(i => i.Price),
                "priceDesc" => query.OrderByDescending(i => i.Price),
                "titleAsc" => query.OrderBy(i => i.Title),
                "ratingDesc" => query.OrderByDescending(i => i.AverageRating ?? 0).ThenByDescending(i => i.ReviewCount),
                "recommended" => query.OrderByDescending(i => i.ItemScore),
                _ => query.OrderByDescending(i => i.ItemScore) // default sort is now by ranking score
            };
        }

        private async Task SetViewBagDataAsync(
            string? search,
            string[]? category,
            decimal? minPrice,
            decimal? maxPrice,
            string? city,
            DateTime? startDate,
            DateTime? endDate,
            string? sort,
            int? minRating,
            int page,
            int totalItems,
            string? currentUserId)
        {
            var absoluteMaxPrice = await CalculateMaxPriceAsync();

            ViewBag.AbsoluteMaxPrice = absoluteMaxPrice;
            ViewBag.Cities = CityData.Cities.Select(c => c.Name).ToList();
            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.City = city;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Sort = sort;
            ViewBag.MinRating = minRating;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)DefaultPageSize);
            ViewBag.TotalItems = totalItems;
            ViewBag.CurrentUserId = currentUserId;
        }

        private async Task<decimal> CalculateMaxPriceAsync()
        {
            var hasItems = await _context.Items.AnyAsync();
            var maxBasePrice = hasItems
                ? await _context.Items.MaxAsync(i => i.Price) ?? 0
                : 1000;

            maxBasePrice = Math.Ceiling(maxBasePrice / 10m) * 10m;
            if (maxBasePrice < 100) maxBasePrice = 500;

            return _currencyService.Convert(maxBasePrice);
        }

        /// <summary>
        /// Creates a new rental request for an item.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRental(int itemId, DateTime startDate, DateTime endDate, List<int>? accessoryIds)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Validate dates
            if (startDate.Date < DateTime.Today)
            {
                return HandleError(_localizer["Start date cannot be in the past."].Value);
            }
            if (endDate.Date < startDate.Date)
            {
                return HandleError(_localizer["End date must be on or after start date."].Value);
            }

            var item = await _context.Items
                .Include(i => i.Rentals)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            // Validate item availability
            if (item == null || !item.IsListed)
            {
                return HandleError(_localizer["Item not available for rent."].Value);
            }

            // Prevent self-rental
            if (item.UserId == currentUser.Id)
            {
                return HandleError(_localizer["You cannot rent your own item."].Value);
            }

            // Check for date conflicts
            if (HasDateConflict(item.Rentals, startDate, endDate))
            {
                return HandleError(_localizer["Item is already booked during this period."].Value);
            }

            // Enforce MaxRentalDays limit
            if (item.MaxRentalDays.HasValue)
            {
                var rentalDays = (endDate.Date - startDate.Date).Days + 1;
                if (rentalDays > item.MaxRentalDays.Value)
                {
                    return HandleError(_localizer["Rental duration exceeds the maximum of {0} days.", item.MaxRentalDays.Value].Value);
                }
            }

            var rental = CreateRentalRequest(item, currentUser.Id, startDate, endDate);
            _context.Rentals.Add(rental);
            await _context.SaveChangesAsync();

            // Attach selected accessories
            if (accessoryIds?.Count > 0)
            {
                var rentalAccessories = await _accessoryService.AttachAccessoriesToRentalAsync(rental.Id, accessoryIds);
                var accessoryTotal = rentalAccessories.Sum(ra => ra.DailyPrice * Math.Max((endDate.Date - startDate.Date).Days + 1, 1));
                rental.TotalPrice += accessoryTotal;
                await _context.SaveChangesAsync();
            }

            await NotifyOwnerOfRentalRequestAsync(item, rental, currentUser);

            return HandleSuccess(
                _localizer["Rental request submitted. Awaiting owner approval."].Value,
                new { success = true, message = _localizer["Rental request submitted successfully."].Value });
        }

        private Rental CreateRentalRequest(Item item, string renterId, DateTime startDate, DateTime endDate)
        {
            return new Rental
            {
                ItemId = item.Id,
                OwnerId = item.UserId ?? string.Empty,
                RenterId = renterId,
                StartDate = startDate,
                EndDate = endDate,
                Status = RentalStatus.Pending,
                TotalPrice = CalculateTotalPrice(item.Price, startDate, endDate)
            };
        }

        private async Task NotifyOwnerOfRentalRequestAsync(Item item, Rental rental, ApplicationUser renter)
        {
            await _hubContext.Clients.User(item.UserId!).SendAsync("RentalRequested", new
            {
                rentalId = rental.Id,
                itemTitle = item.Title,
                renterEmail = renter.Email,
                startDate = rental.StartDate.ToShortDateString(),
                endDate = rental.EndDate.ToShortDateString(),
                status = rental.Status.ToString()
            });

            await _notificationService.CreateAsync(
                item.UserId!,
                NotificationType.RentalRequested,
                _localizer["Notification.RentalRequested"].Value,
                string.Format(_localizer["NotificationMsg.RentalRequested"].Value,
                    renter.FirstName ?? renter.UserName, item.Title),
                rental.Id, "Rental", "/Dashboard?tab=lending");
        }

        /// <summary>
        /// Approves a pending rental request. Owner only.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRental(int rentalId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var rental = await LoadRentalWithItemAsync(rentalId);
            if (rental?.Item == null)
            {
                return HandleError(_localizer["Rental not found."].Value);
            }

            if (rental.OwnerId != currentUser.Id)
            {
                return HandleError(_localizer["You are not authorized to approve this rental."].Value);
            }

            rental.Status = RentalStatus.Accepted;
            rental.Item.IsRented = true;
            rental.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();



            var message = string.Format(_localizer["Your rental request for '{0}' was approved!"], rental.Item.Title);
            await NotifyRentalStatusChangeAsync(rental, message);

            TempData["SuccessMessage"] = string.Format(_localizer["You approved rental for '{0}'."], rental.Item.Title);
            return RedirectToAction(DashboardAction, DashboardController);
        }

        /// <summary>
        /// Marks a rental as completed. Either party can complete.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteRental(int rentalId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var rental = await LoadRentalWithItemAsync(rentalId);
            if (rental == null)
            {
                return HandleError(_localizer["Rental not found."].Value);
            }

            if (!IsUserAuthorizedForRental(currentUser, rental))
            {
                return HandleError(_localizer["You are not authorized to complete this rental."].Value);
            }

            if (rental.Status != RentalStatus.Active && rental.Status != RentalStatus.Accepted)
            {
                return HandleError(_localizer["Only active rentals can be completed."].Value);
            }

            rental.Status = RentalStatus.Completed;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;
            if (DateTime.UtcNow < rental.EndDate)
                rental.EndDate = DateTime.UtcNow;

            // Update LastActivityDate for freshness scoring
            rental.Item.LastActivityDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Event-driven: recompute scores after rental completion
            _ = Task.Run(async () =>
            {
                await _scoringService.ComputeAndSaveItemScoreAsync(rental.ItemId);
                if (rental.OwnerId != null)
                    await _scoringService.ComputeAndSaveProfileTrustScoreAsync(rental.OwnerId);
                await _scoringService.ComputeAndSaveProfileTrustScoreAsync(rental.RenterId);
            });

            var message = string.Format(_localizer["Rental for '{0}' was marked as completed."], rental.Item.Title);
            await NotifyRentalStatusChangeAsync(rental, message);

            TempData["SuccessMessage"] = string.Format(_localizer["Rental for '{0}' completed successfully."], rental.Item.Title);
            return RedirectToAction(DashboardAction, DashboardController);
        }

        /// <summary>
        /// Cancels a rental. Either party can cancel unless completed.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRental(int rentalId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var rental = await LoadRentalWithItemAsync(rentalId);
            if (rental == null)
            {
                return HandleError(_localizer["Rental not found."].Value);
            }

            if (!IsUserAuthorizedForRental(currentUser, rental))
            {
                return HandleError(_localizer["You are not authorized to cancel this rental."].Value);
            }

            if (rental.Status == RentalStatus.Completed)
            {
                return HandleError(_localizer["Completed rentals cannot be cancelled."].Value);
            }

            // Release any authorized deposit before cancellation
            if (rental.Status == RentalStatus.Active || rental.Status == RentalStatus.Accepted)
            {
                var deposit = await _context.RentalDeposits
                    .FirstOrDefaultAsync(d => d.RentalId == rentalId && d.Status == DepositStatus.Authorized);
                if (deposit != null)
                {
                    deposit.Status = DepositStatus.Released;
                    deposit.ReleasedAt = DateTime.UtcNow;
                    deposit.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Deposit for rental {RentalId} auto-released due to cancellation", rentalId);
                }
            }

            rental.Status = RentalStatus.Cancelled;
            rental.Item!.IsRented = false;
            rental.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var message = string.Format(_localizer["Rental for '{0}' was cancelled."], rental.Item.Title);
            await NotifyRentalStatusChangeAsync(rental, message);

            TempData["SuccessMessage"] = _localizer["Rental cancelled successfully."].Value;
            return RedirectToAction(DashboardAction, DashboardController);
        }

        private async Task<Rental?> LoadRentalWithItemAsync(int rentalId)
        {
            return await _context.Rentals
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == rentalId);
        }

        /// <summary>
        /// Lists rentals where the current user is the renter.
        /// </summary>
        public async Task<IActionResult> MyRentals()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var rentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.RenterId == currentUser.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(rentals);
        }

        /// <summary>
        /// Lists rentals for items owned by the current user.
        /// </summary>
        public async Task<IActionResult> OwnerRentals()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var rentals = await _context.Rentals
                .Include(r => r.Item)
                .Where(r => r.OwnerId == currentUser.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(rentals);
        }
    }
}

