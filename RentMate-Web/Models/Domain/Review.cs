using System.ComponentModel.DataAnnotations;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents a review for an item in the RentMate system.
    /// </summary>
    public class Review
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the item being reviewed.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// Foreign key to the reviewer (ApplicationUser).
        /// </summary>
        public string? ReviewerId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Body { get; set; }

        public bool IsAnonymous { get; set; }
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Optional link to the rental that allowed this review.
        /// </summary>
        public int? RentalId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties for Entity Framework
        public virtual ApplicationUser? Reviewer { get; set; }
        public virtual Item? Item { get; set; }
        public virtual Rental? Rental { get; set; }
    }
}
