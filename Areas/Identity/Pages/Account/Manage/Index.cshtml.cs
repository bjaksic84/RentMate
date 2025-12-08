using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RentMate.Models;
using RentMate.Services; // ✅ Tvoj namespace za servise

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IFileUploadService _fileUploadService; // ✅ Servis

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IFileUploadService fileUploadService) // ✅ Dependency Injection
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _fileUploadService = fileUploadService;
        }

        public string? Username { get; set; }

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

            // ✅ URL za prikaz obstoječe slike
            public string? ProfilePictureUrl { get; set; }

            // ✅ Polje za nalaganje nove slike
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
                await LoadAsync(user);
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

            // ✅ Posodobitev osnovnih podatkov
            if (Input.FirstName != user.FirstName) user.FirstName = Input.FirstName;
            if (Input.LastName != user.LastName) user.LastName = Input.LastName;
            if (Input.City != user.City) user.City = Input.City;

            // ✅ LOGIKA ZA SLIKO (Cloudinary)
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