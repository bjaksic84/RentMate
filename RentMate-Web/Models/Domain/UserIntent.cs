namespace RentMate.Models.Domain;

/// <summary>
/// What the user primarily wants to do on RentMate.
/// Collected during onboarding Step 1 to personalize the experience.
/// </summary>
public enum UserIntent
{
    Renter,
    Lister,
    Both
}
