using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
public class OnboardingController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IFileUploadService _fileUploadService;
    private readonly RentMateContext _db;
    private readonly IStringLocalizer<OnboardingController> _localizer;

    private const string ProfileImagesFolder = "profiles";

    public OnboardingController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IFileUploadService fileUploadService,
        RentMateContext db,
        IStringLocalizer<OnboardingController> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _fileUploadService = fileUploadService;
        _db = db;
        _localizer = localizer;
    }

    #region Step 1: Welcome + Intent

    [HttpGet]
    public async Task<IActionResult> Step1()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction(nameof(HomeController.Index), "Home");

        return View(new OnboardingStep1ViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step1(OnboardingStep1ViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid || model.SelectedIntent == null)
            return View(model);

        user.UserIntent = model.SelectedIntent;
        await _userManager.UpdateAsync(user);

        return RedirectToAction(nameof(Step2));
    }

    #endregion

    #region Step 2: Name + Location

    [HttpGet]
    public async Task<IActionResult> Step2()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction(nameof(HomeController.Index), "Home");

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
        var user = await _userManager.GetUserAsync(User);
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
                ModelState.AddModelError(nameof(model.City), _localizer["Invalid city selection."]);
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

        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Step3));
    }

    #endregion

    #region Step 3: Photo + Bio

    [HttpGet]
    public async Task<IActionResult> Step3()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction(nameof(HomeController.Index), "Home");

        // Guard: must complete Step 1 (intent)
        if (user.UserIntent == null)
            return RedirectToAction(nameof(Step1));

        // Guard: must complete Step 2 (first + last name)
        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
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
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            model.ExistingProfilePictureUrl = user.ProfilePictureUrl;
            return View(model);
        }

        // Upload photo if provided
        if (model.ProfilePicture != null)
        {
            var url = await _fileUploadService.UploadFileAsync(model.ProfilePicture, ProfileImagesFolder);
            user.ProfilePictureUrl = url;
        }

        // Save bio if provided
        if (!string.IsNullOrWhiteSpace(model.Bio))
        {
            user.Bio = model.Bio.Trim();
        }

        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Step4));
    }

    #endregion

    #region Step 4: Carousel Tour

    [HttpGet]
    public async Task<IActionResult> Step4()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction(nameof(HomeController.Index), "Home");

        // Guard: must complete Step 1 (intent)
        if (user.UserIntent == null)
            return RedirectToAction(nameof(Step1));

        // Guard: must complete Step 2 (first + last name)
        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
            return RedirectToAction(nameof(Step2));

        var memberCount = await _db.Users.CountAsync();

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
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Idempotency: replays redirect home without re-running side effects.
        if (user.OnboardingCompleted)
            return RedirectToAction(nameof(HomeController.Index), "Home");

        user.OnboardingCompleted = true;
        await _userManager.UpdateAsync(user);

        // Refresh claims so OnboardingCompleted is up to date
        await _signInManager.RefreshSignInAsync(user);

        // Signal spotlight tour for the homepage
        TempData[OnboardingConstants.ShowSpotlightTourKey] = "true";
        TempData[OnboardingConstants.SpotlightIntentKey] = (user.UserIntent ?? UserIntent.Both).ToString();

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    #endregion

    #region Spotlight Tour

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSpotlightComplete()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.SpotlightTourCompleted)
            return NoContent();

        user.SpotlightTourCompleted = true;
        await _userManager.UpdateAsync(user);

        return NoContent();
    }

    #endregion

    #region Helpers

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
