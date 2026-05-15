namespace RentMate.Helpers;

/// <summary>
/// Constants shared between the onboarding controller and consumers
/// (e.g. homepage spotlight tour trigger).
/// </summary>
public static class OnboardingConstants
{
    /// <summary>TempData key set when the spotlight tour should fire on the next homepage render.</summary>
    public const string ShowSpotlightTourKey = "ShowSpotlightTour";

    /// <summary>TempData key holding the user's intent string (Renter / Lister / Both) for the tour.</summary>
    public const string SpotlightIntentKey = "SpotlightIntent";
}
