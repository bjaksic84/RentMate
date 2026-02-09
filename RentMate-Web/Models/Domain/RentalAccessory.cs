using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents an accessory selected for a specific rental.
    /// Stores snapshot values at booking time for price integrity.
    /// </summary>
    public class RentalAccessory
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the rental.
        /// </summary>
        public int RentalId { get; set; }

        /// <summary>
        /// Foreign key to the original item accessory definition.
        /// </summary>
        public int ItemAccessoryId { get; set; }

        /// <summary>
        /// Snapshot of the accessory name at booking time.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Snapshot of the daily price at booking time.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal DailyPrice { get; set; }

        // Navigation properties
        public virtual Rental? Rental { get; set; }
        public virtual ItemAccessory? ItemAccessory { get; set; }
    }
}
