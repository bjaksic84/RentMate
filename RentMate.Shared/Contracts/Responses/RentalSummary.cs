namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Lightweight rental summary for lists.
/// </summary>
public record RentalSummary(
    int Id,
    string ItemTitle,
    int ItemId,
    string? ItemImageUrl,
    string RenterUserName,
    string OwnerUserName,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalPrice,
    RentalStatus Status,
    DateTime CreatedAt
);

/// <summary>
/// Full rental details with related entities.
/// </summary>
public record RentalDetails(
    int Id,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalPrice,
    RentalStatus Status,
    DateTime CreatedAt,
    ItemSummary Item,
    UserSummary Renter,
    UserSummary Owner,
    ReviewSummary? Review
);

/// <summary>
/// Rental status enum - single source of truth.
/// </summary>
public enum RentalStatus
{
    Pending = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3
}
