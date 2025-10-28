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
        public int UserId { get; set; }
        public User? User { get; set; }

        public ICollection<Rental>? Rentals { get; set; }
    }
}
