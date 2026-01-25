namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Lightweight review summary.
/// </summary>
public record ReviewSummary(
    int Id,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    string ReviewerUserName,
    string? ReviewerProfilePictureUrl
);

/// <summary>
/// Full review details with item context.
/// </summary>
public record ReviewDetails(
    int Id,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    int ItemId,
    string ItemTitle,
    UserSummary Reviewer
);
