using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Shared.Contracts.Responses;

namespace RentMate.Services.Extensions
{
    /// <summary>
    /// Extension methods for mapping domain models to DTOs.
    /// Centralizes mapping logic to follow DRY principles.
    /// </summary>
    public static class MappingExtensions
    {
        /// <summary>
        /// Maps an ApplicationUser to a UserSummary DTO.
        /// </summary>
        public static UserSummary ToUserSummary(this ApplicationUser user)
        {
            return new UserSummary(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email,
                user.FirstName,
                user.LastName,
                user.City,
                user.ProfilePictureUrl
            );
        }

        /// <summary>
        /// Maps an Item to an ItemSummary DTO.
        /// </summary>
        public static ItemSummary ToItemSummary(this Item item)
        {
            return new ItemSummary(
                item.Id,
                item.Title ?? "Untitled",
                item.Description,
                item.Price ?? 0,
                item.ImageUrl,
                item.Location,
                item.Category,
                item.IsListed,
                item.IsAdminHidden,
                item.AverageRating ?? 0,
                item.ReviewCount,
                item.CreatedAt
            );
        }

        /// <summary>
        /// Maps an Item with its owner to an ItemWithOwner DTO.
        /// </summary>
        public static ItemWithOwner ToItemWithOwner(this Item item)
        {
            var averageRating = item.Reviews?.Any() == true 
                ? item.Reviews.Average(r => r.Rating) 
                : 0;
            var reviewCount = item.Reviews?.Count ?? 0;
            var location = item.Location ?? item.User?.City;

            return new ItemWithOwner(
                item.Id,
                item.Title ?? string.Empty,
                item.Description,
                item.Price ?? 0,
                item.ImageUrl,
                location,
                item.Category,
                item.IsListed,
                item.IsAdminHidden,
                averageRating,
                reviewCount,
                item.CreatedAt,
                item.UpdatedAt,
                item.User?.ToUserSummary()!
            );
        }

        /// <summary>
        /// Maps a Rental to a RentalDetails DTO.
        /// </summary>
        public static RentalDetails ToRentalDetails(this Rental rental, ReviewSummary? reviewSummary = null)
        {
            return new RentalDetails(
                rental.Id,
                rental.StartDate,
                rental.EndDate,
                rental.TotalPrice,
                rental.Status,
                rental.RentalDate,
                rental.Item?.ToItemSummary()!,
                rental.Renter?.ToUserSummary()!,
                rental.Item?.User?.ToUserSummary()!,
                reviewSummary
            );
        }

        /// <summary>
        /// Maps a Review to a ReviewSummary DTO.
        /// </summary>
        public static ReviewSummary ToReviewSummary(this Review review, ApplicationUser? reviewer = null)
        {
            return new ReviewSummary(
                review.Id,
                review.Rating,
                review.Body,
                review.CreatedAt,
                reviewer?.UserName ?? string.Empty,
                reviewer?.ProfilePictureUrl
            );
        }

        /// <summary>
        /// Calculates aggregate rating statistics from a collection of reviews.
        /// </summary>
        public static (double AverageRating, int ReviewCount) CalculateRatingStats(this IEnumerable<Review>? reviews)
        {
            var activeReviews = reviews?.Where(r => !r.IsDeleted).ToList() ?? new List<Review>();
            var reviewCount = activeReviews.Count;
            var averageRating = activeReviews.Any() 
                ? activeReviews.Average(r => r.Rating) 
                : 0;
            return (averageRating, reviewCount);
        }

        /// <summary>
        /// Gets all non-deleted reviews from a user's items.
        /// </summary>
        public static IEnumerable<Review> GetAllActiveReviews(this ApplicationUser user)
        {
            return (user.Items ?? Enumerable.Empty<Item>())
                .SelectMany(i => i.Reviews ?? Enumerable.Empty<Review>())
                .Where(r => !r.IsDeleted);
        }
    }
}
