using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Services.Interfaces;
using RentMate.Services.Extensions;
using RentMate.Services.Implementations;
using RentMate.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace RentMate.Controllers.Api
{
    /// <summary>
    /// API controller for dashboard data.
    /// Uses IDashboardService for all business logic - same service as MVC controller.
    /// </summary>
    [Route("api/dashboard")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("ApiPolicy")]
    public class DashboardApiController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DashboardApiController> _logger;

        public DashboardApiController(
            IDashboardService dashboardService,
            UserManager<ApplicationUser> userManager,
            ILogger<DashboardApiController> logger)
        {
            _dashboardService = dashboardService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Get the current user's dashboard data.
        /// </summary>
        [HttpGet("userdashboard")]
        [ProducesResponseType(typeof(UserDashboardResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserDashboardResponse>> GetUserDashboard()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var response = await _dashboardService.GetUserDashboardAsync(userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user dashboard for {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving dashboard data");
            }
        }

        /// <summary>
        /// Get rentals where the current user is the renter.
        /// </summary>
        [HttpGet("my-rentals")]
        [ProducesResponseType(typeof(List<RentalSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<RentalSummary>>> GetMyRentals()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var rentals = await _dashboardService.GetMyRentalsAsync(userId);
            return Ok(rentals);
        }

        /// <summary>
        /// Get rentals where the current user is the owner (their items being rented).
        /// </summary>
        [HttpGet("owner-rentals")]
        [ProducesResponseType(typeof(List<RentalSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<RentalSummary>>> GetOwnerRentals()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var rentals = await _dashboardService.GetOwnerRentalsAsync(userId);
            return Ok(rentals);
        }

        /// <summary>
        /// Get admin dashboard data. Requires Admin role.
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(AdminDashboardResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<AdminDashboardResponse>> GetAdminDashboard()
        {
            try
            {
                var response = await _dashboardService.GetAdminDashboardAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin dashboard");
                return StatusCode(500, "An error occurred while retrieving admin dashboard data");
            }
        }

        /// <summary>
        /// Get combined dashboard for users who may also be admins.
        /// </summary>
        [HttpGet("combined")]
        [ProducesResponseType(typeof(CombinedDashboardResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<CombinedDashboardResponse>> GetCombinedDashboard()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var response = await _dashboardService.GetCombinedDashboardAsync(userId, isAdmin);
            
            return Ok(response);
        }

        /// <summary>
        /// Extract user ID from JWT claims.
        /// </summary>
        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                   ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}

