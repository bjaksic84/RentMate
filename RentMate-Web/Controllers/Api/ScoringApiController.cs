using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Controllers.Api;

/// <summary>
/// API controller for marketplace ranking score transparency (§11.4 Seller Score Dashboard).
/// Provides component-level breakdowns of Profile Trust Scores and Item Scores
/// with actionable improvement tips.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("ApiPolicy")]
[Produces("application/json")]
public class ScoringApiController : ControllerBase
{
    private readonly IScoringService _scoringService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RentMateContext _context;

    public ScoringApiController(
        IScoringService scoringService,
        UserManager<ApplicationUser> userManager,
        RentMateContext context)
    {
        _scoringService = scoringService;
        _userManager = userManager;
        _context = context;
    }

    /// <summary>
    /// Get the current user's Profile Trust Score breakdown with improvement tips.
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ProfileTrustBreakdown), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileTrustBreakdown>> GetMyProfileTrustBreakdown()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var breakdown = await _scoringService.GetProfileTrustBreakdownAsync(userId);
        return Ok(breakdown);
    }

    /// <summary>
    /// Get the Item Score breakdown for a specific item owned by the current user.
    /// </summary>
    [HttpGet("item/{itemId}")]
    [ProducesResponseType(typeof(ItemScoreBreakdown), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemScoreBreakdown>> GetItemScoreBreakdown(int itemId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var item = await _context.Items.FindAsync(itemId);
        if (item == null) return NotFound();
        if (item.UserId != userId) return Forbid();

        var breakdown = await _scoringService.GetItemScoreBreakdownAsync(itemId);
        return Ok(breakdown);
    }

    /// <summary>
    /// Force-recompute the current user's profile trust score.
    /// </summary>
    [HttpPost("profile/recompute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecomputeMyProfileScore()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var score = await _scoringService.ComputeAndSaveProfileTrustScoreAsync(userId);
        return Ok(new { profileTrustScore = Math.Round(score, 2) });
    }

    /// <summary>
    /// Force-recompute the score for a specific item owned by the current user.
    /// </summary>
    [HttpPost("item/{itemId}/recompute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecomputeItemScore(int itemId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var item = await _context.Items.FindAsync(itemId);
        if (item == null) return NotFound();
        if (item.UserId != userId) return Forbid();

        var score = await _scoringService.ComputeAndSaveItemScoreAsync(itemId);
        return Ok(new { itemScore = Math.Round(score, 6) });
    }

    /// <summary>
    /// Check if the current user has a review velocity anomaly (§11.1 anti-gaming).
    /// Admin or self-check use only.
    /// </summary>
    [HttpGet("anti-gaming/velocity-check")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckReviewVelocity()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var isAnomaly = await _scoringService.DetectReviewVelocityAnomalyAsync(userId);
        return Ok(new { isAnomaly });
    }
}
