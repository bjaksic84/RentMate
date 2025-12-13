using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RentMate.Models;
using RentMate.Services;
using Microsoft.AspNetCore.Mvc.Rendering; // Za SelectListItem
using RentMate.Helpers; // Za CityData

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IFileUploadService _fileUploadService;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IFileUploadService fileUploadService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _fileUploadService = fileUploadService;
        }

        public string? Username { get; set; }
        
        // Seznam mest za dropdown
        public List<SelectListItem>? CityOptions { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel? Input { get; set; }

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

            // URL za prikaz obstoječe slike
            public string? ProfilePictureUrl { get; set; }

            // Polje za nalaganje nove slike
            [Display(Name = "Slika profila")]
            public IFormFile? NewProfilePicture { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName,
                City = user.City,
                ProfilePictureUrl = user.ProfilePictureUrl // Naložimo iz baze
            };

            // ✅ KORAK 2: Napolnimo dropdown seznam z mesti iz CityData
            CityOptions = CityData.Cities.Select(c => new SelectListItem 
            { 
                Value = c.Name, 
                Text = c.Name,
                Selected = c.Name == user.City // Če ima uporabnik že to mesto, ga označi
            }).ToList();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user); // Če validacija ne uspe, moramo ponovno naložiti mesta!
                return Page();
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
            }

            // Posodobitev osnovnih podatkov
            if (Input.FirstName != user.FirstName) user.FirstName = Input.FirstName;
            if (Input.LastName != user.LastName) user.LastName = Input.LastName;
            
            // ✅ Shranjevanje mesta (prihaja iz dropdowna)
            if (Input.City != user.City) user.City = Input.City;

            // LOGIKA ZA SLIKO (Cloudinary)
            if (Input.NewProfilePicture != null)
            {
                // 1. Nalaganje na Cloudinary (mapa "profiles")
                string newUrl = await _fileUploadService.UploadFileAsync(Input.NewProfilePicture, "profiles");

                // 2. Posodobitev uporabnika
                user.ProfilePictureUrl = newUrl;
            }

            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);
            
            StatusMessage = "Vaš profil je bil posodobljen";
            return RedirectToPage();
        }
    }
}