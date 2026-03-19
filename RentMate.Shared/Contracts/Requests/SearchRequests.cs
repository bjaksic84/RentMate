using System.ComponentModel.DataAnnotations;

namespace RentMate.Shared.Contracts.Requests;

/// <summary>
/// Search/filter request for marketplace items.
/// </summary>
public record SearchItemsRequest
{
    /// <summary>Search term for title/description.</summary>
    public string? SearchQuery { get; init; }
    
    /// <summary>Filter by category.</summary>
    public string? Category { get; init; }
    
    /// <summary>Filter by city.</summary>
    public string? City { get; init; }
    
    /// <summary>Minimum price per day filter.</summary>
    public decimal? MinPrice { get; init; }
    
    /// <summary>Maximum price per day filter.</summary>
    public decimal? MaxPrice { get; init; }
    
    /// <summary>Minimum average rating filter.</summary>
    public double? MinRating { get; init; }
    
    /// <summary>Only show items that are currently available.</summary>
    public bool AvailableOnly { get; init; }
    
    /// <summary>Check availability for specific date range.</summary>
    public DateTime? AvailableFrom { get; init; }
    
    /// <summary>Check availability for specific date range.</summary>
    public DateTime? AvailableTo { get; init; }
    
    /// <summary>Sort by field (newest, price_asc, price_desc, rating, reviews).</summary>
    public string? SortBy { get; init; }
    
    /// <summary>Page number (1-based).</summary>
    public int? Page { get; init; }
    
    /// <summary>Items per page (max 100).</summary>
    public int? PageSize { get; init; }
}

/// <summary>
/// Search/filter request for rentals.
/// </summary>
public record SearchRentalsRequest
{
    /// <summary>Filter by rental status.</summary>
    public RentMate.Shared.Contracts.Responses.RentalStatus? Status { get; init; }
    
    /// <summary>Filter rentals where user is the renter.</summary>
    public bool? AsRenter { get; init; }
    
    /// <summary>Filter rentals where user is the owner.</summary>
    public bool? AsOwner { get; init; }
    
    /// <summary>Filter by start date (rentals starting after this date).</summary>
    public DateTime? FromDate { get; init; }
    
    /// <summary>Filter by end date (rentals ending before this date).</summary>
    public DateTime? ToDate { get; init; }
    
    /// <summary>Sort by field.</summary>
    public string SortBy { get; init; } = "newest";
    
    /// <summary>Page number (1-based).</summary>
    public int Page { get; init; } = 1;
    
    /// <summary>Items per page.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Request to check item availability for a date range.
/// </summary>
public record CheckAvailabilityRequest(
    int ItemId,
    DateTime StartDate,
    DateTime EndDate
);

/// <summary>
/// Request to upload a file/image.
/// </summary>
public record UploadImageRequest
{
    /// <summary>Base64 encoded image data.</summary>
    public string Base64Data { get; init; } = string.Empty;
    
    /// <summary>Original filename with extension.</summary>
    public string FileName { get; init; } = string.Empty;
    
    /// <summary>MIME type (e.g., image/jpeg, image/png).</summary>
    public string ContentType { get; init; } = string.Empty;
}

/// <summary>
/// Request to update review.
/// </summary>
public record UpdateReviewRequest(
    [property: Range(1, 5)] int Rating,
    [property: StringLength(2000)] string? Body
);

/// <summary>
/// Request to cancel a rental.
/// </summary>
public record CancelRentalRequest(
    int RentalId,
    string? Reason
);

/// <summary>
/// Request to extend a rental.
/// </summary>
public record ExtendRentalRequest(
    int RentalId,
    DateTime NewEndDate
);

/// <summary>
/// Request for device registration (push notifications).
/// </summary>
public record RegisterDeviceRequest
{
    /// <summary>Push notification token from device.</summary>
    public string DeviceToken { get; init; } = string.Empty;
    
    /// <summary>Platform (ios, android, web).</summary>
    public string Platform { get; init; } = string.Empty;
    
    /// <summary>Device model/name for debugging.</summary>
    public string? DeviceName { get; init; }
}

/// <summary>
/// Request to report an item or user.
/// </summary>
public record ReportRequest
{
    /// <summary>Type of report (item, user, review).</summary>
    public string ReportType { get; init; } = string.Empty;
    
    /// <summary>ID of the reported entity.</summary>
    public string TargetId { get; init; } = string.Empty;
    
    /// <summary>Reason for the report.</summary>
    public string Reason { get; init; } = string.Empty;
    
    /// <summary>Additional details.</summary>
    public string? Description { get; init; }
}
