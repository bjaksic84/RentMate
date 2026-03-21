using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents a rental transaction in the RentMate system.
    /// </summary>
    public class Rental
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the item being rented.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// Foreign key to the person renting (borrowing).
        /// </summary>
        public string RenterId { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to the owner (person renting out).
        /// </summary>
        public string? OwnerId { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public DateTime RentalDate { get; set; }

        public RentMate.Shared.Contracts.Responses.RentalStatus Status { get; set; } = RentMate.Shared.Contracts.Responses.RentalStatus.Pending;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// When set, the rental has been moved to the history tab.
        /// Null means the rental is still in the active tab (for completed rentals within 7-day grace period).
        /// </summary>
        public DateTime? ArchivedAt { get; set; }

        // Navigation properties for Entity Framework
        public virtual ApplicationUser? Owner { get; set; }
        public virtual ApplicationUser? Renter { get; set; }
        public virtual Item? Item { get; set; }
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<RentalAccessory> Accessories { get; set; } = new List<RentalAccessory>();
        public virtual RentalDeposit? Deposit { get; set; }
        public virtual ICollection<RentalExtension> Extensions { get; set; } = new List<RentalExtension>();
    }
}
