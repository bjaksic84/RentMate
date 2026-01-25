namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Marketplace listing response with filtering metadata.
/// </summary>
public record MarketplaceResponse
{
    public List<ItemWithOwner> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public MarketplaceFilters AppliedFilters { get; init; } = new();
    public List<string> AvailableCities { get; init; } = new();
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
}

/// <summary>
/// Filter criteria for marketplace queries.
/// </summary>
public record MarketplaceFilters
{
    public string? SearchTerm { get; init; }
    public string? City { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public double? MinRating { get; init; }
    public bool? AvailableOnly { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Item detail page response with full context.
/// </summary>
public record ItemDetailResponse
{
    public ItemWithOwner Item { get; init; } = null!;
    public List<ReviewSummary> Reviews { get; init; } = new();
    public List<DateRange> UnavailableDates { get; init; } = new();
    public bool CanRent { get; init; }
    public bool IsOwner { get; init; }
    public bool HasActiveRental { get; init; }
}

/// <summary>
/// Date range for availability checking.
/// </summary>
public record DateRange(DateTime Start, DateTime End);
