using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentMate.Helpers;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for managing user profile information.
    /// Handles identity, bio, location, preferences, and notifications.
    /// </summary>
    public class IndexModel : BaseIdentityPageModel
    {
        #region Constants

        private const string ProfileImagesFolder = "profiles";
        private const string LatitudeFormKey = "Input.Latitude";
        private const string LongitudeFormKey = "Input.Longitude";

        #endregion

        #region Dependencies

        private readonly IFileUploadService _fileUploadService;

        #endregion

        #region Constructor

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IFileUploadService fileUploadService)
            : base(userManager, signInManager)
        {
            _fileUploadService = fileUploadService;
        }

        #endregion

        #region Properties

        /// <summary>Available cities for the dropdown.</summary>
        public List<SelectListItem> CityOptions { get; set; } = new();

        /// <summary>City coordinates JSON for the Leaflet map picker.</summary>
        public string CityCoordinatesJson { get; set; } = "[]";

        /// <summary>Input model for profile updates.</summary>
        [BindProperty]
        public InputModel Input { get; set; } = new();

        #endregion

        #region Input Model

        public class InputModel
        {
            // ── Identity ─────────────────────────────────────────────────
            [Display(Name = "Email")]
            [EmailAddress]
            public string? Email { get; set; }

            [Display(Name = "Username")]
            [StringLength(50, MinimumLength = 3)]
            [RegularExpression(@"^[a-zA-Z0-9._-]+$",
                ErrorMessage = "Username can only contain letters, numbers, dots, underscores and hyphens.")]
            public string? Username { get; set; }

            [Display(Name = "First name")]
            public string? FirstName { get; set; }

            [Display(Name = "Last name")]
            public string? LastName { get; set; }

            // ── Bio ───────────────────────────────────────────────────────
            [Display(Name = "Bio")]
            [StringLength(500)]
            public string? Bio { get; set; }

            // ── Location ─────────────────────────────────────────────────
            [Display(Name = "City")]
            public string? City { get; set; }

            public double? Latitude { get; set; }
            public double? Longitude { get; set; }

            // ── Contact ───────────────────────────────────────────────────
            [Phone]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }

            // ── Preferences ───────────────────────────────────────────────
            [Display(Name = "Return policy")]
            public bool HasReturnPolicy { get; set; }

            [Display(Name = "Preferred language")]
            public string PreferredLanguage { get; set; } = "sl";

            // ── Profile picture ───────────────────────────────────────────
            public string? ProfilePictureUrl { get; set; }

            [Display(Name = "Profile picture")]
            public IFormFile? NewProfilePicture { get; set; }

            // ── Notification preferences ──────────────────────────────────
            public bool NotifyOnRentalRequest { get; set; } = true;
            public bool NotifyOnMessage { get; set; } = true;
            public bool NotifyOnReview { get; set; } = true;
            public bool NotifyOnRentalStatusChange { get; set; } = true;
        }

        #endregion

        #region Page Handlers

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            await LoadUserDataAsync(user!);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            NormalizeCoordinatesFromForm();

            if (IsModelStateInvalid())
            {
                await LoadUserDataAsync(user!);
                return Page();
            }

            var updateResult = await UpdateUserProfileAsync(user!);
            if (!updateResult.Success)
            {
                SetErrorMessage(updateResult.ErrorMessage!);
                await LoadUserDataAsync(user!);
                return Page();
            }

            await UserManager.UpdateAsync(user!);
            await RefreshSignInAsync(user!);

            SetSuccessMessage("Vaš profil je bil posodobljen.");
            return RedirectToPage();
        }

        #endregion

        #region Private Helpers

        /// <summary>Loads all user data into the Input model and page properties.</summary>
        private async Task LoadUserDataAsync(ApplicationUser user)
        {
            var phoneNumber = await UserManager.GetPhoneNumberAsync(user);
            var email = await UserManager.GetEmailAsync(user);
            var username = await UserManager.GetUserNameAsync(user);
            var usernameLocalPart = GetEmailLocalPart(username);
            if (string.IsNullOrWhiteSpace(usernameLocalPart))
                usernameLocalPart = GetEmailLocalPart(email);

            var latitude = user.Latitude;
            var longitude = user.Longitude;
            if (!IsValidCoordinate(latitude, -90, 90) || !IsValidCoordinate(longitude, -180, 180))
            {
                latitude = null;
                longitude = null;
            }

            Input = new InputModel
            {
                Email = email,
                Username = usernameLocalPart,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Bio = user.Bio,
                City = user.City,
                Latitude = latitude,
                Longitude = longitude,
                PhoneNumber = phoneNumber,
                HasReturnPolicy = user.HasReturnPolicy,
                PreferredLanguage = user.PreferredLanguage,
                ProfilePictureUrl = user.ProfilePictureUrl,
                NotifyOnRentalRequest = user.NotifyOnRentalRequest,
                NotifyOnMessage = user.NotifyOnMessage,
                NotifyOnReview = user.NotifyOnReview,
                NotifyOnRentalStatusChange = user.NotifyOnRentalStatusChange,
            };

            LoadCityOptions(user.City);
            LoadCityCoordinatesJson();
        }

        /// <summary>Populates city dropdown from CityData.</summary>
        private void LoadCityOptions(string? currentCity)
        {
            CityOptions = CityData.Cities
                .Select(c => new SelectListItem
                {
                    Value = c.Name,
                    Text = c.Name,
                    Selected = c.Name == currentCity
                })
                .ToList();
        }

        /// <summary>Serializes city coordinates for the Leaflet map picker.</summary>
        private void LoadCityCoordinatesJson()
        {
            var coords = CityData.Cities
                .Select(c => new { name = c.Name, lat = c.Lat, lng = c.Lng });
            CityCoordinatesJson = JsonSerializer.Serialize(coords);
        }

        /// <summary>Orchestrates all profile updates.</summary>
        private async Task<(bool Success, string? ErrorMessage)> UpdateUserProfileAsync(ApplicationUser user)
        {
            // Username uniqueness check
            var usernameResult = await UpdateUsernameAsync(user);
            if (!usernameResult.Success) return usernameResult;

            // Phone number
            var phoneResult = await UpdatePhoneNumberAsync(user);
            if (!phoneResult.Success) return phoneResult;

            // Basic fields (bio, city, lat/lng, preferences, notifications)
            UpdateBasicProfileFields(user);

            // Profile picture
            await UpdateProfilePictureAsync(user);

            // Language cookie side-effect
            if (Input!.PreferredLanguage != user.PreferredLanguage)
                SetLanguageCookie(Input.PreferredLanguage);

            return (true, null);
        }

        /// <summary>
        /// Changes the username if it differs, enforcing global uniqueness.
        /// </summary>
        private async Task<(bool Success, string? ErrorMessage)> UpdateUsernameAsync(ApplicationUser user)
        {
            if (string.IsNullOrWhiteSpace(Input!.Username))
                return (false, "Username is required.");

            var normalizedUsername = GetEmailLocalPart(Input.Username);
            if (string.IsNullOrWhiteSpace(normalizedUsername))
                return (false, "Username is required.");

            Input.Username = normalizedUsername;

            var currentUsername = await UserManager.GetUserNameAsync(user);
            if (string.Equals(normalizedUsername, currentUsername, StringComparison.Ordinal))
                return (true, null);

            var existing = await UserManager.FindByNameAsync(normalizedUsername);
            if (existing != null && existing.Id != user.Id)
                return (false, "That username is already taken. Please choose another.");

            var result = await UserManager.SetUserNameAsync(user, normalizedUsername);
            if (!result.Succeeded)
                return (false, result.Errors.FirstOrDefault()?.Description ?? "Could not update username.");

            return (true, null);
        }

        private static string? GetEmailLocalPart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            var atIndex = trimmed.IndexOf('@');
            return atIndex > -1 ? trimmed[..atIndex] : trimmed;
        }

        /// <summary>
        /// Parses map coordinates from hidden form fields using invariant culture first,
        /// then falls back to current culture for robustness.
        /// </summary>
        private void NormalizeCoordinatesFromForm()
        {
            Input!.Latitude = ParseCoordinateFromForm(LatitudeFormKey, -90, 90, "Latitude");
            Input.Longitude = ParseCoordinateFromForm(LongitudeFormKey, -180, 180, "Longitude");
        }

        private double? ParseCoordinateFromForm(string formKey, double min, double max, string label)
        {
            var raw = Request.Form[formKey].ToString();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var trimmed = raw.Trim();

            var parsed =
                double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

            if (!parsed)
            {
                ModelState.AddModelError(string.Empty, $"Invalid {label} coordinate value.");
                return null;
            }

            if (value < min || value > max)
            {
                ModelState.AddModelError(string.Empty, $"{label} must be between {min} and {max}.");
                return null;
            }

            return Math.Round(value, 6);
        }

        private static bool IsValidCoordinate(double? value, double min, double max)
        {
            if (!value.HasValue)
                return true;

            return value.Value >= min && value.Value <= max;
        }

        /// <summary>Updates the phone number if changed.</summary>
        private async Task<(bool Success, string? ErrorMessage)> UpdatePhoneNumberAsync(ApplicationUser user)
        {
            var current = await UserManager.GetPhoneNumberAsync(user);
            if (Input!.PhoneNumber == current) return (true, null);

            var result = await UserManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            return result.Succeeded
                ? (true, null)
                : (false, "Unexpected error when trying to set phone number.");
        }

        /// <summary>Updates all simple profile fields on the user entity.</summary>
        private void UpdateBasicProfileFields(ApplicationUser user)
        {
            user.FirstName = Input!.FirstName;
            user.LastName = Input.LastName;
            user.Bio = Input.Bio;
            user.City = Input.City;
            user.Latitude = Input.Latitude;
            user.Longitude = Input.Longitude;
            user.HasReturnPolicy = Input.HasReturnPolicy;
            user.PreferredLanguage = Input.PreferredLanguage;
            user.NotifyOnRentalRequest = Input.NotifyOnRentalRequest;
            user.NotifyOnMessage = Input.NotifyOnMessage;
            user.NotifyOnReview = Input.NotifyOnReview;
            user.NotifyOnRentalStatusChange = Input.NotifyOnRentalStatusChange;
        }

        /// <summary>Replaces the profile picture if a new file was uploaded.</summary>
        private async Task UpdateProfilePictureAsync(ApplicationUser user)
        {
            if (Input!.NewProfilePicture == null) return;

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                await _fileUploadService.DeleteFileAsync(user.ProfilePictureUrl);

            user.ProfilePictureUrl = await _fileUploadService.UploadFileAsync(
                Input.NewProfilePicture,
                ProfileImagesFolder);
        }

        /// <summary>
        /// Appends the ASP.NET Core culture cookie so the language change takes effect
        /// on the next request without requiring a controller redirect.
        /// </summary>
        private void SetLanguageCookie(string culture)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
        }

        #endregion
    }
}
