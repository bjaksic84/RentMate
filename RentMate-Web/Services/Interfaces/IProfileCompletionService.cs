namespace RentMate.Services.Interfaces;

/// <summary>
/// Service for evaluating profile completeness and generating actionable
/// improvement suggestions for the incomplete-profile banner and onboarding flow.
/// </summary>
public interface IProfileCompletionService
{
    /// <summary>
    /// Assess the current user's profile completeness.
    /// Returns a lightweight summary optimised for the layout banner.
    /// </summary>
    Task<ProfileCompletionStatus> GetCompletionStatusAsync(string userId);
}

/// <summary>
/// Lightweight profile-completeness summary consumed by the layout banner
/// and the onboarding controller.
/// </summary>
public sealed record ProfileCompletionStatus
{
    /// <summary>Percentage of completeness 0–100.</summary>
    public int Percentage { get; init; }

    /// <summary>True when the post-registration onboarding wizard has been finished.</summary>
    public bool OnboardingCompleted { get; init; }

    /// <summary>First name + last name provided.</summary>
    public bool HasName { get; init; }

    /// <summary>Location / city set.</summary>
    public bool HasLocation { get; init; }

    /// <summary>Profile picture uploaded.</summary>
    public bool HasProfilePicture { get; init; }

    /// <summary>Phone number verified.</summary>
    public bool IsPhoneVerified { get; init; }

    /// <summary>Government ID verified (required to list items).</summary>
    public bool IsGovernmentIdVerified { get; init; }

    /// <summary>Payment method added.</summary>
    public bool HasPaymentMethod { get; init; }

    /// <summary>Bio / about section filled out.</summary>
    public bool HasBio { get; init; }

    /// <summary>
    /// Human-readable improvement tips shown in the banner.
    /// Each entry is a short sentence, e.g. "Add a profile picture".
    /// </summary>
    public IReadOnlyList<ProfileTip> Tips { get; init; } = Array.Empty<ProfileTip>();
}

/// <summary>
/// A single profile-improvement tip with a link to the relevant settings page.
/// </summary>
public sealed record ProfileTip(string Message, string Icon, string Url);
