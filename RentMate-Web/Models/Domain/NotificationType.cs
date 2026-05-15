namespace RentMate.Models.Domain;

/// <summary>
/// Categories of persistent notifications.
/// </summary>
public enum NotificationType
{
    // Rental lifecycle
    RentalRequested,
    RentalAccepted,
    RentalApproved,
    RentalCancelled,
    RentalCompleted,
    RentalOverdue,

    // Extensions
    ExtensionRequested,
    ExtensionApproved,
    ExtensionAutoApproved,
    ExtensionDeclined,
    ExtensionCancelled,
    ExtensionPaid,

    // Deposits & disputes
    DepositCharged,
    DepositReleased,
    DepositDisputed,
    DepositCounterOffered,
    DepositEscalated,
    DepositResolved,
    DeadlineAutoResolved,

    // Social
    ReviewReceived,

    // Admin
    AdminItemHidden,
    AdminDisputeResolved,

    // Payments
    PaymentSucceeded,
    PaymentFailed,
    PaymentRefunded,

    // Account & security
    AccountDeactivationWarning,
    AccountReactivated,
    SecurityAlert,

    // Profile suggestions
    ProfileSuggestion
}

/// <summary>
/// Canonical reference IDs and type string for profile suggestion notifications.
/// Used when creating, detecting duplicates, and auto-dismissing suggestions.
/// </summary>
public static class ProfileSuggestionIds
{
    public const string ReferenceType = "ProfileSuggestion";

    public const int Name = 1;
    public const int Location = 2;
    public const int Photo = 3;
    public const int Phone = 4;
    public const int GovId = 5;
    public const int Payment = 6;
    public const int Bio = 7;

    /// <summary>
    /// Maps Bootstrap icon class (from IProfileCompletionService tips) to referenceId.
    /// </summary>
    public static readonly Dictionary<string, int> ByIcon = new()
    {
        ["bi-person"] = Name,
        ["bi-geo-alt"] = Location,
        ["bi-camera"] = Photo,
        ["bi-phone"] = Phone,
        ["bi-shield-check"] = GovId,
        ["bi-credit-card"] = Payment,
        ["bi-chat-text"] = Bio
    };
}
