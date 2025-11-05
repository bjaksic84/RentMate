using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models
{
    public class Item
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }
        public string? Description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Price { get; set; }

        // Foreign key to owner
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        public bool IsListed { get; set; }  // true = listed publicly for rent
        public bool IsRented { get; set; }  // true = currently being rented

        // 🔹 Optional additions
        public string? Location { get; set; }  // City or pickup location
        public string? ImageUrl { get; set; }  // For future UI use
        public string? Category { get; set; }  // e.g. "Tools", "Vehicles", etc.
        public ICollection<Rental>? Rentals { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
