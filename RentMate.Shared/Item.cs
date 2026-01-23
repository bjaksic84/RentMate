using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Shared
{
    public class Item
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title_Required")]
        [StringLength(100, ErrorMessage = "Title_TooLong")]
        public string? Title { get; set; }
        
        [StringLength(2000, ErrorMessage = "Description_TooLong")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price_Required")]
        [Range(0.01, 1000000, ErrorMessage = "Price_Invalid")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? Price { get; set; }

        // Foreign key to owner
        public string? UserId { get; set; }
        
        public bool IsListed { get; set; }  // true = listed publicly for rent
        public bool IsRented { get; set; }  // true = currently being rented

        // 🔹 Optional additions
        public string? Location { get; set; }  // City or pickup location
        public string? ImageUrl { get; set; }  // For future UI use
        public string? Category { get; set; }  // e.g. "Tools", "Vehicles", etc.
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public double? AverageRating { get; set; } = null;
        public int ReviewCount { get; set; } = 0;

        
    }
}
