using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;

namespace RentMate.Controllers.Api;

/// <summary>
/// API controller for managing user favorites.
/// Uses cookie authentication for browser-based calls.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class FavoritesApiController : ControllerBase
{
    private readonly RentMateContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FavoritesApiController(
        RentMateContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Toggles the favorite status of an item for the current user.
    /// If the item is already favorited, it will be unfavorited; otherwise, it will be favorited.
    /// </summary>
    /// <param name="itemId">The ID of the item to toggle favorite status.</param>
    /// <returns>JSON with the new favorite state.</returns>
    [HttpPost("toggle/{itemId:int}")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int itemId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }

        // Verify the item exists
        var itemExists = await _context.Items.AnyAsync(i => i.Id == itemId);
        if (!itemExists)
        {
            return NotFound(new { success = false, message = "Item not found" });
        }

        // Check if the favorite already exists
        var existingFavorite = await _context.AccountItemFavorites
            .FirstOrDefaultAsync(f => f.AccountId == userId && f.ItemId == itemId);

        bool isFavorited;

        if (existingFavorite != null)
        {
            // Remove the favorite
            _context.AccountItemFavorites.Remove(existingFavorite);
            isFavorited = false;
        }
        else
        {
            // Add the favorite
            var favorite = new AccountItemFavorite
            {
                AccountId = userId,
                ItemId = itemId,
                CreatedAt = DateTime.UtcNow
            };
            _context.AccountItemFavorites.Add(favorite);
            isFavorited = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true, isFavorited });
    }

    /// <summary>
    /// Gets all favorited items for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, message = "User not authenticated" });
        }

        var favorites = await _context.AccountItemFavorites
            .Where(f => f.AccountId == userId)
            .Include(f => f.Item)
            .Select(f => new
            {
                f.ItemId,
                f.Item.Title,
                f.Item.ImageUrl,
                f.Item.Price,
                f.Item.Location,
                f.CreatedAt
            })
            .ToListAsync();

        return Ok(new { success = true, favorites });
    }

    /// <summary>
    /// Checks if a specific item is favorited by the current user.
    /// </summary>
    [HttpGet("check/{itemId:int}")]
    public async Task<IActionResult> CheckFavorite(int itemId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Ok(new { isFavorited = false });
        }

        var isFavorited = await _context.AccountItemFavorites
            .AnyAsync(f => f.AccountId == userId && f.ItemId == itemId);

        return Ok(new { isFavorited });
    }
}

