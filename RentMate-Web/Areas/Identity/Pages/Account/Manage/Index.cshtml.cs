using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentMate.Helpers;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for managing user profile information.
    /// </summary>
    public class IndexModel : BaseIdentityPageModel
    {
        #region Constants

        private const string ProfileImagesFolder = "profiles";
        private const string ProfileUpdatedMessage = "Vaš profil je bil posodobljen";
        private const string PhoneNumberErrorMessage = "Unexpected error when trying to set phone number.";

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

        /// <summary>
        /// Current username for display.
        /// </summary>
        public string? Username { get; set; }
        
        /// <summary>
        /// Available cities for the dropdown.
        /// </summary>
        public List<SelectListItem>? CityOptions { get; set; }

        /// <summary>
        /// Input model for profile updates.
        /// </summary>
        [BindProperty]
        public InputModel? Input { get; set; }

        #endregion

        #region Input Model

        public class InputModel
        {
            [Phone]
            [Display(Name = "Telefonska številka")]
            public string? PhoneNumber { get; set; }

            [Display(Name = "Ime")]
            public string? FirstName { get; set; }

            [Display(Name = "Priimek")]
            public string? LastName { get; set; }

            [Display(Name = "Mesto")]
            public string? City { get; set; }

            /// <summary>
            /// URL of the existing profile picture.
            /// </summary>
            public string? ProfilePictureUrl { get; set; }

            /// <summary>
            /// New profile picture to upload.
            /// </summary>
            [Display(Name = "Slika profila")]
            public IFormFile? NewProfilePicture { get; set; }
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

            if (IsModelStateInvalid())
            {
                await LoadUserDataAsync(user!);
                return Page();
            }

            if (Input == null) return BadRequest();

            var updateResult = await UpdateUserProfileAsync(user!);
            if (!updateResult.Success)
            {
                SetErrorMessage(updateResult.ErrorMessage!);
                return RedirectToPage();
            }

            await UserManager.UpdateAsync(user!);
            await RefreshSignInAsync(user!);
            
            SetSuccessMessage(ProfileUpdatedMessage);
            return RedirectToPage();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Loads all user data for display.
        /// </summary>
        private async Task LoadUserDataAsync(ApplicationUser user)
        {
            Username = await UserManager.GetUserNameAsync(user);
            var phoneNumber = await UserManager.GetPhoneNumberAsync(user);

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName,
                City = user.City,
                ProfilePictureUrl = user.ProfilePictureUrl
            };

            LoadCityOptions(user.City);
        }

        /// <summary>
        /// Populates the city dropdown options.
        /// </summary>
        private void LoadCityOptions(string? currentCity)
        {
            CityOptions = CityData.Cities.Select(c => new SelectListItem 
            { 
                Value = c.Name, 
                Text = c.Name,
                Selected = c.Name == currentCity
            }).ToList();
        }

        /// <summary>
        /// Updates all user profile fields.
        /// </summary>
        private async Task<(bool Success, string? ErrorMessage)> UpdateUserProfileAsync(ApplicationUser user)
        {
            // Update phone number
            var phoneResult = await UpdatePhoneNumberAsync(user);
            if (!phoneResult.Success)
            {
                return phoneResult;
            }

            // Update basic profile fields
            UpdateBasicProfileFields(user);

            // Handle profile picture upload
            await UpdateProfilePictureAsync(user);

            return (true, null);
        }

        /// <summary>
        /// Updates the user's phone number if changed.
        /// </summary>
        private async Task<(bool Success, string? ErrorMessage)> UpdatePhoneNumberAsync(ApplicationUser user)
        {
            var currentPhoneNumber = await UserManager.GetPhoneNumberAsync(user);
            
            if (Input!.PhoneNumber != currentPhoneNumber)
            {
                var setPhoneResult = await UserManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    return (false, PhoneNumberErrorMessage);
                }
            }

            return (true, null);
        }

        /// <summary>
        /// Updates basic profile fields (name, city).
        /// </summary>
        private void UpdateBasicProfileFields(ApplicationUser user)
        {
            if (Input!.FirstName != user.FirstName) 
                user.FirstName = Input.FirstName;
            
            if (Input.LastName != user.LastName) 
                user.LastName = Input.LastName;
            
            if (Input.City != user.City) 
                user.City = Input.City;
        }

        /// <summary>
        /// Uploads a new profile picture if provided.
        /// </summary>
        private async Task UpdateProfilePictureAsync(ApplicationUser user)
        {
            if (Input!.NewProfilePicture != null)
            {
                var newUrl = await _fileUploadService.UploadFileAsync(
                    Input.NewProfilePicture, 
                    ProfileImagesFolder);
                
                user.ProfilePictureUrl = newUrl;
            }
        }

        #endregion
    }
}