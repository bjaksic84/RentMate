namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Lightweight user summary for lists and dropdowns.
/// Does not include navigation properties to avoid circular references.
/// </summary>
public record UserSummary(
    string Id,
    string UserName,
    string? Email,
    string? FirstName,
    string? LastName,
    string? City,
    string? ProfilePictureUrl
);

/// <summary>
/// Extended user info including role information for admin views.
/// </summary>
public record UserWithRoles(
    string Id,
    string UserName,
    string? Email,
    string? FirstName,
    string? LastName,
    string? City,
    string? ProfilePictureUrl,
    List<string> Roles
);
