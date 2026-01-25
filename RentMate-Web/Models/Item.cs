using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models
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

        // Navigation properties for Entity Framework
        public virtual ApplicationUser? User { get; set; }
        public virtual List<Rental> Rentals { get; set; } = new();
        public virtual List<Review> Reviews { get; set; } = new();
    }
}