using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents an item available for rent in the RentMate system.
    /// </summary>
    public class Item
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string? Title { get; set; }
        
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 1000000, ErrorMessage = "Price must be between 0.01 and 1,000,000")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? Price { get; set; }

        /// <summary>
        /// Foreign key to the owner (ApplicationUser).
        /// </summary>
        public string? UserId { get; set; }
        
        public bool IsListed { get; set; }
        public bool IsRented { get; set; }
        public bool IsAdminHidden { get; set; }

        public string? Location { get; set; }
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }

        /// <summary>
        /// Fixed deposit amount required from the renter. Null means no deposit.
        /// </summary>
        [Range(0, 100000, ErrorMessage = "Deposit must be between 0 and 100,000")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? DepositAmount { get; set; }

        /// <summary>
        /// When true, extension requests are auto-approved if no scheduling conflict exists.
        /// </summary>
        public bool AutoApproveExtensions { get; set; }

        /// <summary>
        /// Maximum rental duration in days. Null means no limit.
        /// </summary>
        public int? MaxRentalDays { get; set; }

        // ── Ranking-system fields ───────────────────────────────────
        
        /// <summary>Item condition (e.g. "New", "Like New", "Good", "Fair", "Poor").</summary>
        [StringLength(50)]
        public string? Condition { get; set; }

        /// <summary>Whether the item has explicit calendar/availability set.</summary>
        public bool HasAvailability { get; set; }

        /// <summary>Page-view count in the last 30 days (batch-refreshed).</summary>
        public int ViewsLast30Days { get; set; }

        /// <summary>Total page-view count (incremented on each view).</summary>
        public long TotalViews { get; set; }

        /// <summary>Date of last meaningful activity (rental completed, listing edited, etc.).</summary>
        public DateTime LastActivityDate { get; set; } = DateTime.UtcNow;

        /// <summary>Precomputed Item Score (0.0–1.0) used for default marketplace sort.</summary>
        public double ItemScore { get; set; }

        /// <summary>When the item score was last recalculated.</summary>
        public DateTime? ItemScoreUpdatedAt { get; set; }

        // ── Sponsored / promoted listing ────────────────────────────

        /// <summary>Whether this item is currently being promoted (sponsored).</summary>
        public bool IsSponsored { get; set; }

        /// <summary>Bid amount for sponsored placement (cost-per-rental model).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal? SponsoredBidAmount { get; set; }

        /// <summary>When the sponsored promotion expires.</summary>
        public DateTime? SponsoredUntil { get; set; }

        /// <summary>Latitude for geo-ranking (copied from owner or overridden per-item).</summary>
        public double? Latitude { get; set; }
        /// <summary>Longitude for geo-ranking.</summary>
        public double? Longitude { get; set; }

        // Navigation properties for Entity Framework
        public virtual ApplicationUser? User { get; set; }
        public virtual List<Rental> Rentals { get; set; } = new();
        public virtual List<Review> Reviews { get; set; } = new();
        public virtual List<ItemAccessory> Accessories { get; set; } = new();
        
        /// <summary>
        /// Collection of users who have favorited this item.
        /// Use this to get favorite count: item.FavoritedBy.Count
        /// </summary>
        public virtual List<AccountItemFavorite> FavoritedBy { get; set; } = new();

        /// <summary>
        /// Collection of images for this item.
        /// Images are ordered by DisplayOrder, with 0 being the primary image.
        /// </summary>
        public virtual List<ItemImage> Images { get; set; } = new();

        /// <summary>
        /// Gets the primary image URL (first image by DisplayOrder).
        /// </summary>
        public string? PrimaryImageUrl => Images?.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
    }
}
