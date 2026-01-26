namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Lightweight item summary for lists and cards.
/// </summary>
public record ItemSummary(
    int Id,
    string Title,
    string? Description,
    decimal PricePerDay,
    string? ImageUrl,
    string? City,
    string? Category,
    bool IsListed,
    bool IsAdminHidden,
    double AverageRating,
    int ReviewCount,
    DateTime CreatedAt
);

/// <summary>
/// Item with owner information for marketplace/detail views.
/// </summary>
public record ItemWithOwner(
    int Id,
    string Title,
    string? Description,
    decimal PricePerDay,
    string? ImageUrl,
    string? City,
    string? Category,
    bool IsListed,
    bool IsAdminHidden,
    double AverageRating,
    int ReviewCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    UserSummary Owner
);
