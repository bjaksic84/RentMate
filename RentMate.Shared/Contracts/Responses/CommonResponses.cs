namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Paginated list response wrapper for any list endpoint.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public record PaginatedResponse<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public PaginatedResponse() { }

    public PaginatedResponse(List<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}

/// <summary>
/// Standard API response wrapper for consistent error handling.
/// </summary>
/// <typeparam name="T">The type of the data payload.</typeparam>
public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();
    
    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };
    
    public static ApiResponse<T> Fail(string error) => new()
    {
        Success = false,
        Errors = new List<string> { error }
    };
    
    public static ApiResponse<T> Fail(IEnumerable<string> errors) => new()
    {
        Success = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Non-generic API response for operations that don't return data.
/// </summary>
public record ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();
    
    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message
    };
    
    public static ApiResponse Fail(string error) => new()
    {
        Success = false,
        Errors = new List<string> { error }
    };
}
