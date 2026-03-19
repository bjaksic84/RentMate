using System.ComponentModel.DataAnnotations;
using RentMate.Shared.Contracts.Validation;

namespace RentMate.Shared.Contracts.Requests;

/// <summary>
/// Request to create a new rental.
/// </summary>
public record CreateRentalRequest(
    int ItemId,
    DateTime StartDate,
    DateTime EndDate
);

/// <summary>
/// Request to update rental status.
/// </summary>
public record UpdateRentalStatusRequest(
    int RentalId,
    RentMate.Shared.Contracts.Responses.RentalStatus NewStatus
);

/// <summary>
/// Request to create a new item listing.
/// </summary>
public record CreateItemRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    [Range(0.01, 1000000, ErrorMessage = "Price must be between 0.01 and 1,000,000")]
    public decimal PricePerDay { get; init; }
    [CloudinaryUrl]
    public string? ImageUrl { get; init; }
    public string? City { get; init; }
    public bool IsListed { get; init; } = true;
    public string? Category { get; init; }
}

/// <summary>
/// Request to update an existing item.
/// </summary>
public record UpdateItemRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    [Range(0.01, 1000000, ErrorMessage = "Price must be between 0.01 and 1,000,000")]
    public decimal PricePerDay { get; init; }
    [CloudinaryUrl]
    public string? ImageUrl { get; init; }
    public string? City { get; init; }
    public bool IsListed { get; init; }
    public string? Category { get; init; }
}

/// <summary>
/// Request to create a review.
/// </summary>
public record CreateReviewRequest(
    int ItemId,
    int? RentalId,
    [property: Range(1, 5)] int Rating,
    [property: StringLength(2000)] string? Body
);

/// <summary>
/// Request to process a payment.
/// </summary>
public record CreatePaymentRequest(
    int RentalId,
    string PaymentMethod
);

/// <summary>
/// Request to update user profile.
/// </summary>
public record UpdateProfileRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? City { get; init; }
    public string? PhoneNumber { get; init; }
    [CloudinaryUrl]
    public string? ProfilePictureUrl { get; init; }
}
