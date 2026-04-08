using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RentMate.Controllers.Base;
using RentMate.Helpers;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Services.Interfaces;

namespace RentMate.Controllers.Mvc;

/// <summary>
/// Post-registration onboarding wizard (4 steps + completion).
/// Step 1: Welcome + Intent selection
/// Step 2: Name + optional Location
/// Step 3: Photo + Bio (optional, skippable)
/// Step 4: App Tour Carousel
/// </summary>
[Authorize]
public class OnboardingController : BaseAppController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IFileUploadService _fileUploadService;
    private readonly IProfileCompletionService _profileCompletionService;
    private readonly INotificationService _notificationService;
    private readonly RentMateContext _db;
    private readonly IMemoryCache _cache;

    private const string ProfileImagesFolder = "profiles";

    public OnboardingController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IFileUploadService fileUploadService,
        IProfileCompletionService profileCompletionService,
        INotificationService notificationService,
        RentMateContext db,
        IMemoryCache cache) : base(userManager)
    {
        _signInManager = signInManager;
        _fileUploadService = fileUploadService;
        _profileCompletionService = profileCompletionService;
        _notificationService = notificationService;
        _db = db;
        _cache = cache;
    }

    #region Step 1: Welcome + Intent

    [HttpGet]
    public async Task<IActionResult> Step1()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        return View(new OnboardingStep1ViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step1(OnboardingStep1ViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid || model.SelectedIntent == null)
            return View(model);

        user.UserIntent = model.SelectedIntent;
        await UserManager.UpdateAsync(user);

        return RedirectToAction(nameof(Step2));
    }

    #endregion

    #region Step 2: Name + Location

    [HttpGet]
    public async Task<IActionResult> Step2()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        // Guard: must complete Step 1 (intent)
        if (user.UserIntent == null)
            return RedirectToAction(nameof(Step1));

        var model = new OnboardingStep2ViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            City = user.City,
            CityOptions = BuildCityOptions(user.City)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step2(OnboardingStep2ViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            model.CityOptions = BuildCityOptions(model.City);
            return View(model);
        }

        user.FirstName = model.FirstName?.Trim();
        user.LastName = model.LastName?.Trim();

        if (model.ShareLocation && !string.IsNullOrEmpty(model.City))
        {
            // Validate city against allowlist
            if (!CityData.Cities.Any(c => c.Name == model.City))
            {
                ModelState.AddModelError(nameof(model.City), "Invalid city selection.");
                model.CityOptions = BuildCityOptions(model.City);
                return View(model);
            }

            user.City = model.City;
            var coords = CityData.GetCoordinates(model.City);
            if (coords.Lat != 0 || coords.Lng != 0)
            {
                user.Latitude = coords.Lat;
                user.Longitude = coords.Lng;
            }
        }
        else
        {
            // User declined location sharing
            user.City = null;
            user.Latitude = null;
            user.Longitude = null;
        }

        await UserManager.UpdateAsync(user);
        return RedirectToAction(nameof(Step3));
    }

    #endregion

    #region Step 3: Photo + Bio

    [HttpGet]
    public async Task<IActionResult> Step3()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        // Guard: must complete Step 2 (name)
        if (string.IsNullOrWhiteSpace(user.FirstName))
            return RedirectToAction(nameof(Step2));

        var model = new OnboardingStep3ViewModel
        {
            ExistingProfilePictureUrl = user.ProfilePictureUrl,
            Bio = user.Bio
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step3(OnboardingStep3ViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            model.ExistingProfilePictureUrl = user.ProfilePictureUrl;
            return View(model);
        }

        if (model.ProfilePicture != null)
        {
            var url = await _fileUploadService.UploadFileAsync(model.ProfilePicture, ProfileImagesFolder);
            user.ProfilePictureUrl = url;
        }

        if (!string.IsNullOrWhiteSpace(model.Bio))
            user.Bio = model.Bio.Trim();

        await UserManager.UpdateAsync(user);

        return RedirectToAction(nameof(Step4));
    }

    #endregion

    #region Step 4: Carousel Tour

    [HttpGet]
    public async Task<IActionResult> Step4()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        // Guard: must complete Step 2 (name)
        if (string.IsNullOrWhiteSpace(user.FirstName))
            return RedirectToAction(nameof(Step2));

        var memberCount = await _cache.GetOrCreateAsync("onboarding:memberCount", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _db.Users.CountAsync();
        });

        var model = new OnboardingStep4ViewModel
        {
            UserIntent = user.UserIntent ?? UserIntent.Both,
            FirstName = user.FirstName ?? "there",
            City = user.City,
            ShareLocation = !string.IsNullOrEmpty(user.City),
            MemberCount = memberCount
        };

        return View(model);
    }

    #endregion

    #region Complete Onboarding

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteOnboarding()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        user.OnboardingCompleted = true;
        await UserManager.UpdateAsync(user);

        // Create profile suggestion notifications for incomplete items
        await CreateProfileSuggestionsAsync(user.Id);

        // Refresh claims so OnboardingCompleted is up to date
        await _signInManager.RefreshSignInAsync(user);

        // Signal spotlight tour for the homepage
        TempData["ShowSpotlightTour"] = "true";
        TempData["SpotlightIntent"] = (user.UserIntent ?? UserIntent.Both).ToString();

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSpotlightComplete()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (!user.SpotlightTourCompleted)
        {
            user.SpotlightTourCompleted = true;
            await UserManager.UpdateAsync(user);
        }

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestartTour()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        user.OnboardingCompleted = false;
        user.SpotlightTourCompleted = false;
        user.UserIntent = null;
        await UserManager.UpdateAsync(user);
        await _signInManager.RefreshSignInAsync(user);

        return RedirectToAction(nameof(Step1));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates persistent notifications for each incomplete profile item.
    /// Skips items that already have a non-dismissed notification (handles restart tour).
    /// </summary>
    private async Task CreateProfileSuggestionsAsync(string userId)
    {
        var status = await _profileCompletionService.GetCompletionStatusAsync(userId);
        if (status.Tips.Count == 0) return;

        // Get existing non-dismissed profile suggestions to avoid duplicates
        var existingRefIds = await _db.Notifications
            .Where(n => n.UserId == userId
                && n.Type == NotificationType.ProfileSuggestion
                && n.ReferenceType == ProfileSuggestionIds.ReferenceType
                && !n.IsDismissed)
            .Select(n => n.ReferenceId)
            .ToListAsync();

        foreach (var tip in status.Tips)
        {
            if (!ProfileSuggestionIds.ByIcon.TryGetValue(tip.Icon, out var refId)) continue;
            if (existingRefIds.Contains(refId)) continue;

            await _notificationService.CreateAsync(
                userId,
                NotificationType.ProfileSuggestion,
                tip.Message,
                actionUrl: tip.Url,
                referenceId: refId,
                referenceType: ProfileSuggestionIds.ReferenceType);
        }
    }

    private static List<SelectListItem> BuildCityOptions(string? selectedCity)
    {
        return CityData.Cities.Select(c => new SelectListItem
        {
            Value = c.Name,
            Text = c.Name,
            Selected = c.Name == selectedCity
        }).ToList();
    }

    #endregion
}
