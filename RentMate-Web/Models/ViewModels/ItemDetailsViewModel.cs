namespace RentMate.Models.ViewModels;

using RentMate.Models.Domain;

/// <summary>
/// ViewModel for the Item Details page. Precomputes derived data
/// so partials don't need to query or calculate.
/// </summary>
public class ItemDetailsViewModel
{
    #region Core Item Data
    public int ItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public decimal? DepositAmount { get; set; }
    public int? MaxRentalDays { get; set; }
    public bool AutoApproveExtensions { get; set; }
    public bool IsListed { get; set; }
    #endregion

    #region Images
    public List<ItemImageViewModel> Images { get; set; } = new();
    public string? PrimaryImageUrl { get; set; }
    #endregion

    #region Owner Profile
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerCity { get; set; }
    public string? OwnerProfilePictureUrl { get; set; }
    public DateTime OwnerMemberSince { get; set; }
    public bool OwnerIsPhoneVerified { get; set; }
    public bool OwnerIsGovernmentIdVerified { get; set; }
    public double OwnerResponseRate { get; set; }
    public double OwnerAvgResponseTimeHours { get; set; }
    public int OwnerCompletedRentals { get; set; }
    public double OwnerAverageRating { get; set; }
    public double OwnerTrustScore { get; set; }
    #endregion

    #region Reviews
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int ItemRentalCount { get; set; }
    public int[] StarCounts { get; set; } = new int[5]; // Index 0 = 1-star, Index 4 = 5-star
    public List<ReviewViewModel> Reviews { get; set; } = new();
    public bool CanReview { get; set; }
    public bool IsSignedIn { get; set; }
    #endregion

    #region Accessories
    public List<AccessoryViewModel> Accessories { get; set; } = new();
    #endregion

    #region Availability
    public List<RentalDateRange> BlockedDateRanges { get; set; } = new();
    #endregion

    #region Similar Items
    public List<SimilarItemViewModel> SimilarItems { get; set; } = new();
    #endregion

    #region Map
    public double? MapLat { get; set; }
    public double? MapLng { get; set; }
    public string? MapCityName { get; set; }
    #endregion

    #region User Context
    public string? CurrentUserId { get; set; }
    public bool IsFavorited { get; set; }
    public bool IsOwner { get; set; }
    #endregion

    #region Modal Support
    /// <summary>
    /// The raw Item entity, needed by _RentModal partial which expects @model Item.
    /// Only used for passing to the modal — all other partials use ViewModel properties.
    /// </summary>
    public Item Item { get; set; } = null!;
    #endregion
}

public class ItemImageViewModel
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class ReviewViewModel
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsAnonymous { get; set; }
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public string? ReviewerProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AccessoryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DailyPrice { get; set; }
}

public class RentalDateRange
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class SimilarItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public string? City { get; set; }
    public string? Category { get; set; }
}
