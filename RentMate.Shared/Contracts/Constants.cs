namespace RentMate.Shared.Contracts;

/// <summary>
/// Shared constants for the RentMate platform.
/// These values are the single source of truth across Web API and mobile clients.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Item categories available in the platform.
    /// </summary>
    public static class Categories
    {
        public const string Tools = "Tools";
        public const string Electronics = "Electronics";
        public const string Sports = "Sport";
        public const string HomeAndGarden = "Home";
        public const string Events = "Events";
        public const string Other = "Other";
        
        public static readonly IReadOnlyList<string> All = new[]
        {
            Tools, Electronics, Sports, HomeAndGarden, Events, Other
        };
        
        public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
        {
            { Tools, "🔧 Tools" },
            { Electronics, "📱 Electronics" },
            { Sports, "⚽ Sports" },
            { HomeAndGarden, "🏠 Home & Garden" },
            { Events, "🎉 Events & Fun" },
            { Other, "📦 Other" }
        };
    }
    
    /// <summary>
    /// User roles in the system.
    /// </summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string User = "User";
        
        public static readonly IReadOnlyList<string> All = new[] { Admin, User };
    }
    
    /// <summary>
    /// Supported payment methods.
    /// </summary>
    public static class PaymentMethods
    {
        public const string Cash = "Cash";
        public const string Card = "Card";
        public const string BankTransfer = "BankTransfer";
        public const string PayPal = "PayPal";
        
        public static readonly IReadOnlyList<string> All = new[]
        {
            Cash, Card, BankTransfer, PayPal
        };
    }
    
    /// <summary>
    /// Sorting options for marketplace listings.
    /// </summary>
    public static class SortOptions
    {
        public const string Newest = "newest";
        public const string PriceLowToHigh = "price_asc";
        public const string PriceHighToLow = "price_desc";
        public const string Rating = "rating";
        public const string MostReviews = "reviews";
        
        public static readonly IReadOnlyList<string> All = new[]
        {
            Newest, PriceLowToHigh, PriceHighToLow, Rating, MostReviews
        };
    }
    
    /// <summary>
    /// Validation constraints.
    /// </summary>
    public static class Validation
    {
        public const int TitleMinLength = 3;
        public const int TitleMaxLength = 100;
        public const int DescriptionMaxLength = 2000;
        public const decimal MinPricePerDay = 0.01m;
        public const decimal MaxPricePerDay = 10000m;
        public const int MinRating = 1;
        public const int MaxRating = 5;
        public const int ReviewCommentMaxLength = 1000;
        public const int MinRentalDays = 1;
        public const int MaxRentalDays = 365;
        public const int PasswordMinLength = 6;
        public const int UsernameMinLength = 3;
        public const int UsernameMaxLength = 50;
    }
    
    /// <summary>
    /// API pagination defaults.
    /// </summary>
    public static class Pagination
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;
        public const int DefaultPage = 1;
    }
}
