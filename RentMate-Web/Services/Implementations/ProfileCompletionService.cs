using Microsoft.AspNetCore.Identity;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations;

/// <summary>
/// Evaluates how complete a user's profile is and generates improvement tips.
/// Used by the floating banner partial and the onboarding controller.
/// </summary>
public class ProfileCompletionService : IProfileCompletionService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileCompletionService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ProfileCompletionStatus> GetCompletionStatusAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new ProfileCompletionStatus { Percentage = 0 };
        }

        var hasName = !string.IsNullOrWhiteSpace(user.FirstName) &&
                      !string.IsNullOrWhiteSpace(user.LastName);
        var hasLocation = !string.IsNullOrWhiteSpace(user.City);
        var hasProfilePicture = !string.IsNullOrWhiteSpace(user.ProfilePictureUrl);
        var isPhoneVerified = user.IsPhoneVerified;
        var isGovernmentIdVerified = user.IsGovernmentIdVerified;
        var hasPaymentMethod = user.HasPaymentMethodAdded;
        var hasBio = !string.IsNullOrWhiteSpace(user.Bio);

        // ── Weighted percentage ──────────────────────────────────────
        // Each checkpoint has a weight; total = 100.
        const int wName = 15;
        const int wLocation = 20;
        const int wPicture = 15;
        const int wPhone = 15;
        const int wGovId = 15;
        const int wPayment = 10;
        const int wBio = 10;

        int score = 0;
        if (hasName) score += wName;
        if (hasLocation) score += wLocation;
        if (hasProfilePicture) score += wPicture;
        if (isPhoneVerified) score += wPhone;
        if (isGovernmentIdVerified) score += wGovId;
        if (hasPaymentMethod) score += wPayment;
        if (hasBio) score += wBio;

        // ── Build tips (ordered by impact / priority) ────────────────
        var tips = new List<ProfileTip>();

        if (!hasName)
            tips.Add(new ProfileTip("Add your first and last name", "bi-person", "/Identity/Account/Manage"));

        if (!hasLocation)
            tips.Add(new ProfileTip("Set your location", "bi-geo-alt", "/Identity/Account/Manage"));

        if (!hasProfilePicture)
            tips.Add(new ProfileTip("Upload a profile picture", "bi-camera", "/Identity/Account/Manage"));

        if (!isPhoneVerified)
            tips.Add(new ProfileTip("Verify your phone number", "bi-phone", "/Identity/Account/Manage"));

        if (!isGovernmentIdVerified)
            tips.Add(new ProfileTip("Verify your government ID to list items", "bi-shield-check", "/Identity/Account/Manage"));

        if (!hasPaymentMethod)
            tips.Add(new ProfileTip("Add a payment method", "bi-credit-card", "/Payment/Settings"));

        if (!hasBio)
            tips.Add(new ProfileTip("Write a short bio about yourself", "bi-chat-text", "/Identity/Account/Manage"));

        return new ProfileCompletionStatus
        {
            Percentage = score,
            OnboardingCompleted = user.OnboardingCompleted,
            HasName = hasName,
            HasLocation = hasLocation,
            HasProfilePicture = hasProfilePicture,
            IsPhoneVerified = isPhoneVerified,
            IsGovernmentIdVerified = isGovernmentIdVerified,
            HasPaymentMethod = hasPaymentMethod,
            HasBio = hasBio,
            Tips = tips
        };
    }
}
