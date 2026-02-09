using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents a request to extend a rental's end date.
    /// Requires owner approval by default; owners can enable auto-approve per item.
    /// </summary>
    public class RentalExtension
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the rental being extended.
        /// </summary>
        public int RentalId { get; set; }

        /// <summary>
        /// Foreign key to the user who requested the extension.
        /// </summary>
        public string RequestedByUserId { get; set; } = string.Empty;

        /// <summary>
        /// The rental's end date before this extension.
        /// </summary>
        [DataType(DataType.Date)]
        public DateTime OriginalEndDate { get; set; }

        /// <summary>
        /// The requested new end date.
        /// </summary>
        [DataType(DataType.Date)]
        public DateTime NewEndDate { get; set; }

        /// <summary>
        /// Additional cost for the extension period.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal AdditionalCost { get; set; }

        /// <summary>
        /// The daily rate used to calculate the additional cost.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal DailyRate { get; set; }

        /// <summary>
        /// Current status of the extension request.
        /// </summary>
        public ExtensionStatus Status { get; set; } = ExtensionStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Rental? Rental { get; set; }
        public virtual ApplicationUser? RequestedBy { get; set; }
    }
}
