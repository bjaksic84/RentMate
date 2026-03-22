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
    SecurityAlert
}
