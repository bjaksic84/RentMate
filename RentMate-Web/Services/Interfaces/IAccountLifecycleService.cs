using RentMate.Models.Domain;

namespace RentMate.Services.Interfaces;

/// <summary>
/// Centralises all account lifecycle operations: deactivation, reactivation, and deletion.
/// </summary>
public interface IAccountLifecycleService
{
    /// <summary>Returns true if the user has any active, pending, or accepted rentals.</summary>
    Task<bool> HasActiveRentalsAsync(string userId);

    /// <summary>
    /// Deactivates the account: hides the profile, delists all items, and invalidates the
    /// security stamp so the user is signed out. Reversible.
    /// </summary>
    /// <param name="userId">ID of the account to deactivate.</param>
    /// <param name="source">Whether initiated by the user or an admin.</param>
    /// <param name="reason">Optional admin-provided reason displayed to the user.</param>
    Task DeactivateAccountAsync(string userId, DeactivationSource source, string? reason = null);

    /// <summary>
    /// Reactivates a previously deactivated account: restores visibility and re-lists items.
    /// Only valid when the account was user-deactivated; admin-deactivated accounts must go
    /// through the dispute/ticket flow.
    /// </summary>
    Task ReactivateAccountAsync(string userId);

    /// <summary>
    /// Permanently anonymises the account (GDPR right to erasure).
    /// All PII is cleared; items are hard-deleted; anonymised transaction records are preserved.
    /// Throws <see cref="InvalidOperationException"/> when active rentals exist.
    /// </summary>
    Task DeleteAccountAsync(string userId);
}
