namespace RentMate.Models.Domain;

/// <summary>
/// Indicates who initiated an account deactivation.
/// </summary>
public enum DeactivationSource
{
    /// <summary>The user deactivated their own account. They can self-reactivate.</summary>
    User,

    /// <summary>An admin deactivated the account. Reactivation requires a support request.</summary>
    Admin
}
