using System.ComponentModel.DataAnnotations;
using RentMate.Models.Domain;

namespace RentMate.Models.ViewModels;

/// <summary>
/// Step 1: Welcome + Intent selection.
/// </summary>
public class OnboardingStep1ViewModel
{
    /// <summary>Selected intent (posted via hidden field when a card is clicked).</summary>
    [Required]
    public UserIntent? SelectedIntent { get; set; }
}

/// <summary>
/// Step 2: Name + optional Location.
/// </summary>
public class OnboardingStep2ViewModel
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    /// <summary>Whether the user wants to share location. Defaults to true.</summary>
    public bool ShareLocation { get; set; } = true;

    [Display(Name = "Country")]
    public string? Country { get; set; }

    [Display(Name = "State / Region")]
    public string? State { get; set; }

    [Display(Name = "City")]
    public string? City { get; set; }

    /// <summary>Populated by controller for the city dropdown.</summary>
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> CityOptions { get; set; } = new();
}

/// <summary>
/// Step 3: Photo + Bio (both optional).
/// </summary>
public class OnboardingStep3ViewModel
{
    public string? ExistingProfilePictureUrl { get; set; }

    [Display(Name = "Profile Picture")]
    public IFormFile? ProfilePicture { get; set; }

    [StringLength(500)]
    [Display(Name = "About you")]
    public string? Bio { get; set; }
}

/// <summary>
/// Step 4: Carousel tour. Read-only data for the view.
/// </summary>
public class OnboardingStep4ViewModel
{
    public UserIntent UserIntent { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? City { get; set; }
    public bool ShareLocation { get; set; }
    public int MemberCount { get; set; }
}
