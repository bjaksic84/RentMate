using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentMate.Models.Domain;

namespace RentMate.Controllers.Base
{
    /// <summary>
    /// Base controller providing common helper methods for MVC controllers.
    /// Reduces code duplication across controllers.
    /// </summary>
    public abstract class BaseAppController : Controller
    {
        #region Dependencies

        protected readonly UserManager<ApplicationUser> UserManager;

        #endregion

        #region Constructor

        protected BaseAppController(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        #endregion

        #region User Helpers

        /// <summary>
        /// Gets the current user asynchronously.
        /// </summary>
        protected Task<ApplicationUser?> GetCurrentUserAsync()
        {
            return UserManager.GetUserAsync(User);
        }

        /// <summary>
        /// Gets the current user's ID.
        /// </summary>
        protected string? GetCurrentUserId()
        {
            return UserManager.GetUserId(User);
        }

        /// <summary>
        /// Verifies the user is authenticated and returns the user if valid.
        /// Returns null and sets appropriate response if not authenticated.
        /// </summary>
        protected async Task<(ApplicationUser? User, IActionResult? ErrorResult)> EnsureAuthenticatedAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return (null, Unauthorized());
            }
            return (user, null);
        }

        #endregion

        #region Request Helpers

        /// <summary>
        /// Checks if the current request is an AJAX request.
        /// </summary>
        protected bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }

        #endregion

        #region Response Helpers

        /// <summary>
        /// Returns an appropriate error response based on whether this is an AJAX request.
        /// </summary>
        protected IActionResult HandleError(
            string message, 
            string redirectAction = "UserDashboard", 
            string redirectController = "Dashboard")
        {
            if (IsAjaxRequest())
            {
                return BadRequest(message);
            }

            TempData["ErrorMessage"] = message;
            return RedirectToAction(redirectAction, redirectController);
        }

        /// <summary>
        /// Returns an appropriate success response based on whether this is an AJAX request.
        /// </summary>
        protected IActionResult HandleSuccess(
            string message, 
            object? jsonData = null, 
            string redirectAction = "UserDashboard", 
            string redirectController = "Dashboard")
        {
            if (IsAjaxRequest())
            {
                return Json(jsonData ?? new { success = true, message });
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(redirectAction, redirectController);
        }

        /// <summary>
        /// Redirects to dashboard with an error message.
        /// </summary>
        protected IActionResult RedirectToDashboardWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("UserDashboard", "Dashboard");
        }

        /// <summary>
        /// Redirects to dashboard with a success message.
        /// </summary>
        protected IActionResult RedirectToDashboardWithSuccess(string message)
        {
            TempData["SuccessMessage"] = message;
            return RedirectToAction("UserDashboard", "Dashboard");
        }

        #endregion

        #region Authorization Helpers

        /// <summary>
        /// Checks if the current user owns the specified item.
        /// </summary>
        protected bool IsItemOwner(string userId, Item item)
        {
            return item.UserId == userId;
        }

        /// <summary>
        /// Checks if the current user is in the Admin role.
        /// </summary>
        protected Task<bool> IsCurrentUserAdminAsync()
        {
            return IsUserInRoleAsync("Admin");
        }

        /// <summary>
        /// Checks if the current user is in the specified role.
        /// </summary>
        protected async Task<bool> IsUserInRoleAsync(string role)
        {
            var user = await GetCurrentUserAsync();
            return user != null && await UserManager.IsInRoleAsync(user, role);
        }

        #endregion
    }

    /// <summary>
    /// Base controller providing common helper methods for API controllers.
    /// Reduces code duplication across API controllers.
    /// </summary>
    public abstract class BaseApiController : ControllerBase
    {
        #region Dependencies

        protected readonly UserManager<ApplicationUser> UserManager;
        protected readonly ILogger Logger;

        #endregion

        #region Constructor

        protected BaseApiController(UserManager<ApplicationUser> userManager, ILogger logger)
        {
            UserManager = userManager;
            Logger = logger;
        }

        #endregion

        #region User Helpers

        /// <summary>
        /// Gets the current user's ID with fallback to claims.
        /// </summary>
        protected string? GetCurrentUserId()
        {
            var userId = UserManager.GetUserId(User);
            
            // Fallback to direct claim if UserManager fails
            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }

            return userId;
        }

        /// <summary>
        /// Ensures the user is authenticated and returns the user ID.
        /// Returns null if not authenticated.
        /// </summary>
        protected (string? UserId, ActionResult? ErrorResult) EnsureAuthenticated()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return (null, Unauthorized());
            }
            return (userId, null);
        }

        #endregion

        #region Authorization Helpers

        /// <summary>
        /// Checks if the specified user owns the item.
        /// </summary>
        protected bool IsItemOwner(string userId, Item item)
        {
            return item.UserId == userId;
        }

        /// <summary>
        /// Checks if the specified user owns the rental (either as renter or owner).
        /// </summary>
        protected bool IsRentalParticipant(string userId, Rental rental)
        {
            return rental.RenterId == userId || rental.OwnerId == userId;
        }

        #endregion

        #region Logging Helpers

        /// <summary>
        /// Logs an unauthorized access attempt.
        /// </summary>
        protected void LogUnauthorizedAccess(string userId, int itemId, string? ownerId, string operation)
        {
            Logger.LogWarning(
                "User {UserId} attempted to {Operation} item {ItemId} owned by {OwnerId}",
                userId, operation, itemId, ownerId);
        }

        /// <summary>
        /// Logs a successful operation.
        /// </summary>
        protected void LogOperation(string operation, int entityId, string userId)
        {
            Logger.LogInformation(
                "{Operation} for entity {EntityId} by user {UserId}",
                operation, entityId, userId);
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validates that an entity ID is positive.
        /// </summary>
        protected bool IsValidId(int id) => id > 0;

        /// <summary>
        /// Creates a standard not found response with a message.
        /// </summary>
        protected NotFoundObjectResult NotFoundWithMessage(string message)
        {
            return NotFound(new { error = message });
        }

        /// <summary>
        /// Creates a standard bad request response with a message.
        /// </summary>
        protected BadRequestObjectResult BadRequestWithMessage(string message)
        {
            return BadRequest(new { error = message });
        }

        #endregion
    }
}

