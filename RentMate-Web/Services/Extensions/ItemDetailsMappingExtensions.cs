using RentMate.Helpers;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;

namespace RentMate.Services.Extensions;

/// <summary>
/// Maps a fully-loaded Item entity to ItemDetailsViewModel.
/// Extracted from ItemsController.Details to keep the action readable.
/// </summary>
public static class ItemDetailsMappingExtensions
{
    public static ItemDetailsViewModel ToDetailsViewModel(
        this Item item,
        string? currentUserId,
        int ownerCompletedRentals,
        double ownerAverageRating,
        int[] starCounts,
        bool canReview,
        List<SimilarItemViewModel> similarItems,
        CityInfo cityCoordinates)
    {
        var ownerName = item.User != null
            ? $"{item.User.FirstName} {item.User.LastName}".Trim()
            : string.Empty;

        var isOwner = currentUserId != null && item.UserId == currentUserId;

        return new ItemDetailsViewModel
        {
            // Core item data
            ItemId = item.Id,
            Title = item.Title ?? string.Empty,
            Description = item.Description,
            Price = item.Price ?? 0,
            Category = item.Category,
            DepositAmount = item.DepositAmount,
            MaxRentalDays = item.MaxRentalDays,
            AutoApproveExtensions = item.AutoApproveExtensions,
            IsListed = item.IsListed,

            // Images
            Images = item.Images.Select(img => new ItemImageViewModel
            {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                DisplayOrder = img.DisplayOrder
            }).ToList(),
            PrimaryImageUrl = item.PrimaryImageUrl,

            // Owner profile
            OwnerId = item.UserId ?? string.Empty,
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? (item.User?.UserName ?? string.Empty) : ownerName,
            OwnerCity = item.User?.City,
            OwnerProfilePictureUrl = item.User?.ProfilePictureUrl,
            OwnerMemberSince = item.User?.CreatedAt ?? DateTime.UtcNow,
            OwnerIsPhoneVerified = item.User?.IsPhoneVerified ?? false,
            OwnerIsGovernmentIdVerified = item.User?.IsGovernmentIdVerified ?? false,
            OwnerResponseRate = item.User?.ResponseRate ?? 0,
            OwnerAvgResponseTimeHours = item.User?.AvgResponseTimeHours ?? 0,
            OwnerCompletedRentals = ownerCompletedRentals,
            OwnerAverageRating = ownerAverageRating,
            OwnerTrustScore = item.User?.ProfileTrustScore ?? 0,

            // Reviews
            AverageRating = item.AverageRating,
            ReviewCount = item.ReviewCount,
            ItemRentalCount = item.Rentals.Count,
            StarCounts = starCounts,
            Reviews = item.Reviews.OrderByDescending(r => r.CreatedAt).Select(r => new ReviewViewModel
            {
                Id = r.Id,
                Rating = r.Rating,
                Title = r.Title,
                Body = r.Body,
                IsAnonymous = r.IsAnonymous,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.IsAnonymous ? null : (r.Reviewer != null
                    ? (string.IsNullOrWhiteSpace($"{r.Reviewer.FirstName} {r.Reviewer.LastName}".Trim())
                        ? r.Reviewer.UserName
                        : $"{r.Reviewer.FirstName} {r.Reviewer.LastName}".Trim())
                    : null),
                ReviewerProfilePictureUrl = r.IsAnonymous ? null : r.Reviewer?.ProfilePictureUrl,
                CreatedAt = r.CreatedAt
            }).ToList(),
            CanReview = canReview,
            IsSignedIn = currentUserId != null,

            // Accessories
            Accessories = item.Accessories.Select(a => new AccessoryViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                DailyPrice = a.DailyPrice
            }).ToList(),

            // Blocked date ranges
            BlockedDateRanges = item.Rentals.Select(r => new RentalDateRange
            {
                StartDate = r.StartDate,
                EndDate = r.EndDate
            }).ToList(),

            // Similar items
            SimilarItems = similarItems,

            // Map
            MapLat = cityCoordinates.Lat,
            MapLng = cityCoordinates.Lng,
            MapCityName = cityCoordinates.Name,

            // User context
            CurrentUserId = currentUserId,
            IsFavorited = item.FavoritedBy.Any(),
            IsOwner = isOwner,

            // Modal support (raw entity)
            Item = item
        };
    }
}
