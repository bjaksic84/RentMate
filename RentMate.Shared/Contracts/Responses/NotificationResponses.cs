namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Notification for the user.
/// </summary>
public record NotificationResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public NotificationType Type { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
    
    /// <summary>Related entity ID (item, rental, etc.) for navigation.</summary>
    public string? TargetId { get; init; }
    
    /// <summary>Type of target entity for proper navigation.</summary>
    public string? TargetType { get; init; }
}

/// <summary>
/// Types of notifications.
/// </summary>
public enum NotificationType
{
    /// <summary>General system notification.</summary>
    System = 0,
    
    /// <summary>New rental request received (for item owners).</summary>
    RentalRequest = 1,
    
    /// <summary>Rental was approved by owner.</summary>
    RentalApproved = 2,
    
    /// <summary>Rental was rejected by owner.</summary>
    RentalRejected = 3,
    
    /// <summary>Rental period is starting soon.</summary>
    RentalStarting = 4,
    
    /// <summary>Rental period is ending soon.</summary>
    RentalEnding = 5,
    
    /// <summary>Rental has been completed.</summary>
    RentalCompleted = 6,
    
    /// <summary>New review received on your item.</summary>
    NewReview = 7,
    
    /// <summary>Payment received.</summary>
    PaymentReceived = 8,
    
    /// <summary>Payment reminder.</summary>
    PaymentReminder = 9,
    
    /// <summary>New message in chat.</summary>
    NewMessage = 10
}

/// <summary>
/// Paginated list of notifications.
/// </summary>
public record NotificationsResponse
{
    public List<NotificationResponse> Notifications { get; init; } = new();
    public int UnreadCount { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>
/// Availability check response.
/// </summary>
public record AvailabilityResponse
{
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public List<DateRange> ConflictingDates { get; init; } = new();
    
    /// <summary>Calculated total price for the requested period.</summary>
    public decimal? TotalPrice { get; init; }
    
    /// <summary>Number of days in the rental period.</summary>
    public int? RentalDays { get; init; }
}

/// <summary>
/// Image upload response.
/// </summary>
public record ImageUploadResponse
{
    public bool Success { get; init; }
    public string? Url { get; init; }
    public string? PublicId { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Statistics response for analytics views.
/// </summary>
public record StatisticsResponse
{
    /// <summary>Label for the time period.</summary>
    public string Period { get; init; } = string.Empty;
    
    /// <summary>New users in period.</summary>
    public int NewUsers { get; init; }
    
    /// <summary>New listings in period.</summary>
    public int NewListings { get; init; }
    
    /// <summary>Rentals created in period.</summary>
    public int RentalsCreated { get; init; }
    
    /// <summary>Revenue generated in period.</summary>
    public decimal Revenue { get; init; }
}

/// <summary>
/// Platform statistics summary.
/// </summary>
public record PlatformStatsResponse
{
    public int TotalUsers { get; init; }
    public int TotalItems { get; init; }
    public int TotalRentals { get; init; }
    public int ActiveRentals { get; init; }
    public decimal TotalRevenue { get; init; }
    public double AveragePlatformRating { get; init; }
    public List<StatisticsResponse> MonthlyStats { get; init; } = new();
    public Dictionary<string, int> ItemsByCategory { get; init; } = new();
    public List<string> TopCities { get; init; } = new();
}

/// <summary>
/// User statistics for their profile.
/// </summary>
public record UserStatsResponse
{
    public int ItemsListed { get; init; }
    public int ItemsRented { get; init; }
    public int TotalRentalsAsOwner { get; init; }
    public int TotalRentalsAsRenter { get; init; }
    public decimal TotalEarnings { get; init; }
    public decimal TotalSpent { get; init; }
    public double AverageRating { get; init; }
    public int TotalReviewsReceived { get; init; }
    public int TotalReviewsGiven { get; init; }
    public DateTime MemberSince { get; init; }
}

/// <summary>
/// City data for location selection.
/// </summary>
public record CityResponse(
    string Name,
    string? Region,
    int ItemCount
);

/// <summary>
/// Category data with counts.
/// </summary>
public record CategoryResponse(
    string Id,
    string Name,
    string Icon,
    int ItemCount
);

/// <summary>
/// Home/landing page response with featured content.
/// </summary>
public record HomeResponse
{
    public List<ItemWithOwner> FeaturedItems { get; init; } = new();
    public List<ItemWithOwner> NewItems { get; init; } = new();
    public List<ItemWithOwner> PopularItems { get; init; } = new();
    public List<CategoryResponse> Categories { get; init; } = new();
    public List<CityResponse> PopularCities { get; init; } = new();
    public int TotalItemsAvailable { get; init; }
}

/// <summary>
/// Public user profile response (what other users can see).
/// </summary>
public record PublicProfileResponse
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? City { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public DateTime MemberSince { get; init; }
    public double AverageRating { get; init; }
    public int TotalReviews { get; init; }
    public int TotalListings { get; init; }
    public List<ItemSummary> ActiveListings { get; init; } = new();
    public List<ReviewSummary> RecentReviews { get; init; } = new();
}

/// <summary>
/// Chat/message thread summary.
/// </summary>
public record MessageThreadResponse
{
    public string ThreadId { get; init; } = string.Empty;
    public UserSummary OtherUser { get; init; } = null!;
    public ItemSummary? RelatedItem { get; init; }
    public string LastMessage { get; init; } = string.Empty;
    public DateTime LastMessageAt { get; init; }
    public int UnreadCount { get; init; }
}

/// <summary>
/// Individual message in a thread.
/// </summary>
public record MessageResponse
{
    public string Id { get; init; } = string.Empty;
    public string SenderId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public bool IsRead { get; init; }
}
