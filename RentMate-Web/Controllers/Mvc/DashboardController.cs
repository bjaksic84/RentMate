using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Services.Interfaces;
using RentMate.Services.Extensions;
using RentMate.Services.Implementations;
using RentMate.Shared.Contracts.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.SignalR;
using RentMate.Hubs;

namespace RentMate.Controllers.Mvc
{
    /// <summary>
    /// MVC Controller for dashboard views.
    /// Uses IDashboardService for business logic, with legacy mapping for views.
    /// </summary>
    [Authorize]
    public class DashboardController : Controller
    {
        #region Constants

        /// <summary>Maximum number of items to display in dashboard lists.</summary>
        private const int DashboardListLimit = 10;
        private const string AdminRole = "Admin";
        private const string DashboardErrorMessage = "An error occurred while loading the dashboard.";

        #endregion

        #region Dependencies

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RentMateContext _context;
        private readonly IDashboardService _dashboardService;
        private readonly IDepositService _depositService;
        private readonly IRentalExtensionService _extensionService;
        private readonly IHubContext<RentMateHub> _hubContext;
        private readonly ILogger<DashboardController> _logger;
        private readonly IScoringService _scoringService;

        #endregion

        #region Constructor

        public DashboardController(
            UserManager<ApplicationUser> userManager,
            RentMateContext context,
            IDashboardService dashboardService,
            IDepositService depositService,
            IRentalExtensionService extensionService,
            IHubContext<RentMateHub> hubContext,
            ILogger<DashboardController> logger,
            IScoringService scoringService)
        {
            _userManager = userManager;
            _context = context;
            _dashboardService = dashboardService;
            _depositService = depositService;
            _extensionService = extensionService;
            _hubContext = hubContext;
            _logger = logger;
            _scoringService = scoringService;
        }

        #endregion

        #region Public Actions

        /// <summary>
        /// Redirects to the appropriate dashboard based on user role.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            
            if (user != null && await _userManager.IsInRoleAsync(user, AdminRole))
            {
                return RedirectToAction(nameof(AdminDashboard));
            }

            return RedirectToAction(nameof(UserDashboard));
        }

        /// <summary>
        /// Displays the admin dashboard with platform-wide statistics.
        /// </summary>
        [Authorize(Roles = AdminRole)]
        public async Task<IActionResult> AdminDashboard()
        {
            try
            {
                var response = await _dashboardService.GetAdminDashboardAsync();
                var viewModel = response.ToLegacyViewModel();
                
                await PopulateAdminDashboardEntitiesAsync(viewModel);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                return HandleDashboardError();
            }
        }

        /// <summary>
        /// Displays the user dashboard with personal statistics and data.
        /// </summary>
        [Authorize]
        public async Task<IActionResult> UserDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var response = await _dashboardService.GetUserDashboardAsync(user.Id);
                var viewModel = response.ToLegacyViewModel();
                
                await PopulateUserDashboardEntitiesAsync(viewModel, user.Id);
                SetUserDashboardDebugInfo(user.Id, viewModel.ListingsOwned?.Count ?? 0);

                // Scoring dashboard data
                ViewBag.ProfileTrustScore = user.ProfileTrustScore;
                ViewBag.ProfileTrustBreakdown = await _scoringService.GetProfileTrustBreakdownAsync(user.Id);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user dashboard for {UserId}", user.Id);
                return HandleDashboardError();
            }
        }

        /// <summary>
        /// Handles rental extension requests from the dashboard.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestExtension(int RentalId, DateTime NewEndDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var extension = await _extensionService.RequestExtensionAsync(RentalId, user.Id, NewEndDate);
                var isAutoApproved = extension.Status == ExtensionStatus.AutoApproved;

                // Notify the owner about the extension request
                var rental = await _context.Rentals.Include(r => r.Item).FirstOrDefaultAsync(r => r.Id == RentalId);
                if (rental != null)
                {
                    await _hubContext.Clients.User(rental.OwnerId!).SendAsync(RentMateHub.ExtensionRequestedEvent, new
                    {
                        extensionId = extension.Id,
                        rentalId = RentalId,
                        itemTitle = rental.Item?.Title,
                        newEndDate = extension.NewEndDate.ToString("yyyy-MM-dd"),
                        autoApproved = isAutoApproved
                    });
                }

                return Json(new
                {
                    success = true,
                    message = isAutoApproved ? "Extension auto-approved!" : "Extension request sent.",
                    autoApproved = isAutoApproved
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension request failed for rental {RentalId}", RentalId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Approves a pending extension request (owner action).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveExtension(int extensionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                await _extensionService.ApproveExtensionAsync(extensionId, user.Id);

                var ext = await _context.RentalExtensions.Include(e => e.Rental).ThenInclude(r => r!.Item).FirstOrDefaultAsync(e => e.Id == extensionId);
                if (ext?.Rental != null)
                {
                    await _hubContext.Clients.User(ext.RequestedByUserId!).SendAsync(RentMateHub.ExtensionStatusChangedEvent, new
                    {
                        extensionId, status = "Accepted", itemTitle = ext.Rental.Item?.Title, newEndDate = ext.NewEndDate.ToString("yyyy-MM-dd"),
                        additionalCost = ext.AdditionalCost
                    });
                }

                return Json(new { success = true, message = "Extension accepted — awaiting renter payment." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension approval failed for {ExtensionId}", extensionId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Declines a pending extension request (owner action).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineExtension(int extensionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                await _extensionService.DeclineExtensionAsync(extensionId, user.Id);

                var decExt = await _context.RentalExtensions.Include(e => e.Rental).ThenInclude(r => r!.Item).FirstOrDefaultAsync(e => e.Id == extensionId);
                if (decExt?.Rental != null)
                {
                    await _hubContext.Clients.User(decExt.RequestedByUserId!).SendAsync(RentMateHub.ExtensionStatusChangedEvent, new
                    {
                        extensionId, status = "Declined", itemTitle = decExt.Rental.Item?.Title
                    });
                }

                return Json(new { success = true, message = "Extension declined." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension decline failed for {ExtensionId}", extensionId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cancels an accepted extension (renter decided not to pay).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelExtension(int extensionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var extension = await _extensionService.CancelExtensionAsync(extensionId, user.Id);

                var cancelExt = await _context.RentalExtensions.Include(e => e.Rental).ThenInclude(r => r!.Item).FirstOrDefaultAsync(e => e.Id == extensionId);
                if (cancelExt?.Rental != null)
                {
                    await _hubContext.Clients.User(cancelExt.Rental.OwnerId!).SendAsync(RentMateHub.ExtensionStatusChangedEvent, new
                    {
                        extensionId, status = "CancelledByRenter", itemTitle = cancelExt.Rental.Item?.Title
                    });
                }

                return Json(new { success = true, message = "Extension cancelled." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension cancel failed for {ExtensionId}", extensionId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Archives a completed/cancelled rental, moving it to the history tab.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveRental(int rentalId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var rental = await _context.Rentals
                    .Include(r => r.Deposit)
                    .FirstOrDefaultAsync(r => r.Id == rentalId);

                if (rental == null)
                    return Json(new { success = false, message = "Rental not found." });

                if (rental.RenterId != user.Id && rental.OwnerId != user.Id)
                    return Json(new { success = false, message = "Not authorized." });

                if (rental.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Completed
                    && rental.Status != RentMate.Shared.Contracts.Responses.RentalStatus.Cancelled)
                    return Json(new { success = false, message = "Only completed or cancelled rentals can be archived." });

                // Don't allow archiving if there's an active deposit dispute
                if (rental.Deposit != null && (rental.Deposit.Status == DepositStatus.Disputed
                    || rental.Deposit.Status == DepositStatus.CounterOffered
                    || rental.Deposit.Status == DepositStatus.Escalated))
                    return Json(new { success = false, message = "Cannot archive while deposit dispute is active." });

                rental.ArchivedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Rental moved to history." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Archive rental failed for {RentalId}", rentalId);
                return Json(new { success = false, message = "Something went wrong." });
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Populates entity lists needed by the admin dashboard view.
        /// </summary>
        private async Task PopulateAdminDashboardEntitiesAsync(DashboardViewModel viewModel)
        {
            viewModel.Users = await _userManager.Users
                .AsNoTracking()
                .Take(DashboardListLimit)
                .ToListAsync();

            viewModel.Listings = await BuildRecentItemsQuery()
                .Take(DashboardListLimit)
                .ToListAsync();

            viewModel.Rentals = await BuildRecentRentalsQuery(includeAllParties: true)
                .Take(DashboardListLimit)
                .ToListAsync();

            viewModel.EscalatedDisputes = await _depositService.GetEscalatedDisputesAsync();
        }

        /// <summary>
        /// Populates entity lists needed by the user dashboard view.
        /// </summary>
        private async Task PopulateUserDashboardEntitiesAsync(DashboardViewModel viewModel, string userId)
        {
            viewModel.ListingsOwned = await _context.Items
                .AsNoTracking()
                .Include(i => i.Accessories)
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            viewModel.MyRentals = await BuildUserRentalsQuery(userId, asRenter: true)
                .ToListAsync();

            viewModel.OwnerRentals = await BuildUserRentalsQuery(userId, asRenter: false)
                .ToListAsync();

            viewModel.FavoriteItems = await BuildUserFavoritesQuery(userId)
                .ToListAsync();

            viewModel.PendingExtensions = await _extensionService
                .GetPendingExtensionsForOwnerAsync(userId);
            viewModel.PendingExtensionCount = viewModel.PendingExtensions.Count;

            viewModel.DepositSummary = await _depositService
                .GetDepositSummaryForOwnerAsync(userId);
        }

        /// <summary>
        /// Builds query for recent items with owner information.
        /// </summary>
        private IQueryable<Item> BuildRecentItemsQuery()
        {
            return _context.Items
                .AsNoTracking()
                .Include(i => i.User)
                .OrderByDescending(i => i.CreatedAt);
        }

        /// <summary>
        /// Builds query for recent rentals with related entities.
        /// </summary>
        private IQueryable<Rental> BuildRecentRentalsQuery(bool includeAllParties)
        {
            IQueryable<Rental> query = _context.Rentals
                .AsNoTracking()
                .Include(r => r.Item);

            if (includeAllParties)
            {
                query = query
                    .Include(r => r.Renter)
                    .Include(r => r.Owner);
            }

            return query.OrderByDescending(r => r.CreatedAt);
        }

        /// <summary>
        /// Builds query for user's rentals (either as renter or owner).
        /// </summary>
        private IQueryable<Rental> BuildUserRentalsQuery(string userId, bool asRenter)
        {
            IQueryable<Rental> query = _context.Rentals
                .AsNoTracking()
                .Include(r => r.Item)
                    .ThenInclude(i => i!.Images)
                .Include(r => r.Item)
                    .ThenInclude(i => i!.Reviews.Where(rev => !rev.IsDeleted))
                .Include(r => r.Deposit).ThenInclude(d => d!.Evidence).ThenInclude(e => e.SubmittedBy)
                .Include(r => r.Accessories)
                .Include(r => r.Extensions.Where(e => e.Status == ExtensionStatus.Pending || e.Status == ExtensionStatus.Accepted || e.Status == ExtensionStatus.AutoApproved));

            if (asRenter)
            {
                query = query
                    .Include(r => r.Owner)
                    .Include(r => r.Item!.User)
                    .Where(r => r.RenterId == userId);
            }
            else
            {
                query = query
                    .Include(r => r.Renter)
                    .Where(r => r.OwnerId == userId);
            }

            return query.OrderByDescending(r => r.StartDate);
        }

        /// <summary>
        /// Builds query for user's favorite items.
        /// </summary>
        private IQueryable<Item> BuildUserFavoritesQuery(string userId)
        {
            return _context.AccountItemFavorites
                .AsNoTracking()
                .Where(f => f.AccountId == userId)
                .Include(f => f.Item)
                    .ThenInclude(i => i.Images)
                .Include(f => f.Item)
                    .ThenInclude(i => i.User)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Item);
        }

        /// <summary>
        /// Handles dashboard loading errors by redirecting to home with error message.
        /// </summary>
        private IActionResult HandleDashboardError()
        {
            TempData["ErrorMessage"] = DashboardErrorMessage;
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Sets debug information for the user dashboard view.
        /// </summary>
        private void SetUserDashboardDebugInfo(string userId, int itemCount)
        {
            ViewData["DebugUserId"] = userId;
            ViewData["FoundItems"] = itemCount;
        }

        #endregion
    }
}

