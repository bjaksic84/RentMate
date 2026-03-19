using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentMate.Helpers;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Controllers.Mvc;

/// <summary>
/// Post-registration onboarding wizard.
/// Step 1: First name, Last name, Location (required).
/// Step 2: Profile picture (optional / skippable).
/// </summary>
[Authorize]
public class OnboardingController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IFileUploadService _fileUploadService;

    private const string ProfileImagesFolder = "profiles";

    public OnboardingController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IFileUploadService fileUploadService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _fileUploadService = fileUploadService;
    }

    // ── Step 1: Name + Location ─────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Step1()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Already completed onboarding → go home
        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        ViewBag.CityOptions = BuildCityOptions(user.City);

        var model = new OnboardingStep1ViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            City = user.City
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step1(OnboardingStep1ViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            ViewBag.CityOptions = BuildCityOptions(model.City);
            return View(model);
        }

        user.FirstName = model.FirstName?.Trim();
        user.LastName = model.LastName?.Trim();

        // Validate city against allowlist
        if (!string.IsNullOrEmpty(model.City) &&
            !CityData.Cities.Any(c => c.Name == model.City))
        {
            ModelState.AddModelError(nameof(model.City), "Invalid city selection.");
            ViewBag.CityOptions = BuildCityOptions(model.City);
            return View(model);
        }

        user.City = model.City;

        // Set lat/lng from city data
        var coords = CityData.GetCoordinates(model.City);
        if (coords.Lat != 0 || coords.Lng != 0)
        {
            user.Latitude = coords.Lat;
            user.Longitude = coords.Lng;
        }

        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Step2));
    }

    // ── Step 2: Profile Picture ─────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Step2()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.OnboardingCompleted)
            return RedirectToAction("Index", "Home");

        // Guard: must complete step 1 first
        if (string.IsNullOrWhiteSpace(user.City))
            return RedirectToAction(nameof(Step1));

        var model = new OnboardingStep2ViewModel
        {
            ExistingProfilePictureUrl = user.ProfilePictureUrl
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step2(OnboardingStep2ViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (model.ProfilePicture != null)
        {
            var url = await _fileUploadService.UploadFileAsync(model.ProfilePicture, ProfileImagesFolder);
            user.ProfilePictureUrl = url;
        }

        // Mark onboarding as completed
        user.OnboardingCompleted = true;
        await _userManager.UpdateAsync(user);

        // Refresh the sign-in cookie so the new claims / name are visible immediately
        await _signInManager.RefreshSignInAsync(user);

        TempData["SuccessMessage"] = "Welcome to RentMate! Your profile is all set.";
        return RedirectToAction("Index", "Home");
    }

    // ── Skip Step 2 ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SkipStep2()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.OnboardingCompleted = true;
        await _userManager.UpdateAsync(user);
        await _signInManager.RefreshSignInAsync(user);

        TempData["SuccessMessage"] = "Welcome to RentMate! You can complete your profile anytime in Settings.";
        return RedirectToAction("Index", "Home");
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static List<SelectListItem> BuildCityOptions(string? selectedCity)
    {
        return CityData.Cities.Select(c => new SelectListItem
        {
            Value = c.Name,
            Text = c.Name,
            Selected = c.Name == selectedCity
        }).ToList();
    }
}

// ── View Models ────────────────────────────────────────────────────

public class OnboardingStep1ViewModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "First name is required.")]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    [System.ComponentModel.DataAnnotations.Display(Name = "First Name")]
    public string? FirstName { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Last name is required.")]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Location is required.")]
    [System.ComponentModel.DataAnnotations.Display(Name = "Location")]
    public string? City { get; set; }
}

public class OnboardingStep2ViewModel
{
    public string? ExistingProfilePictureUrl { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Profile Picture")]
    public IFormFile? ProfilePicture { get; set; }
}
