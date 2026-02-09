using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents an accessory that can be added to an item rental.
    /// Defined by the item owner as inline name + daily price entries.
    /// </summary>
    public class ItemAccessory
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the parent item.
        /// </summary>
        public int ItemId { get; set; }

        [Required(ErrorMessage = "Accessory name is required")]
        [StringLength(100, ErrorMessage = "Accessory name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Daily price is required")]
        [Range(0.01, 100000, ErrorMessage = "Daily price must be between 0.01 and 100,000")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal DailyPrice { get; set; }

        public bool IsAvailable { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Item? Item { get; set; }
        public virtual List<RentalAccessory> RentalAccessories { get; set; } = new();
    }
}
