namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Authentication response after successful login.
/// </summary>
public record AuthResponse
{
    public bool Success { get; init; }
    public string? Token { get; init; }
    public DateTime? Expiration { get; init; }
    public UserSummary? User { get; init; }
    public List<string> Roles { get; init; } = new();
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();
    
    public static AuthResponse Successful(string token, DateTime expiration, UserSummary user, List<string> roles) => new()
    {
        Success = true,
        Token = token,
        Expiration = expiration,
        User = user,
        Roles = roles
    };
    
    public static AuthResponse Failed(string error) => new()
    {
        Success = false,
        Errors = new List<string> { error }
    };
    
    public static AuthResponse Failed(IEnumerable<string> errors) => new()
    {
        Success = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Profile response for the current user.
/// </summary>
public record ProfileResponse
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? City { get; init; }
    public string? PhoneNumber { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public List<string> Roles { get; init; } = new();
    public DateTime? MemberSince { get; init; }
    
    // Statistics
    public int TotalListings { get; init; }
    public int TotalRentals { get; init; }
    public double AverageRating { get; init; }
    public int TotalReviews { get; init; }
}
