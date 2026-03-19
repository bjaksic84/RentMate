using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Tracks the deposit authorization lifecycle for a rental.
    /// Uses an authorization hold model — deposit is held but not charged
    /// unless the owner reports damage or non-return.
    /// </summary>
    public class RentalDeposit
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the rental (one-to-one relationship).
        /// </summary>
        public int RentalId { get; set; }

        /// <summary>
        /// The deposit amount authorized.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Current status of the deposit.
        /// </summary>
        public DepositStatus Status { get; set; } = DepositStatus.Pending;

        /// <summary>
        /// Amount actually charged from the deposit (null if not charged).
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal? ChargedAmount { get; set; }

        /// <summary>
        /// Reason provided by the owner for charging the deposit.
        /// </summary>
        [StringLength(1000)]
        public string? ChargeReason { get; set; }

        /// <summary>
        /// Opaque reference from the payment provider for future integration.
        /// </summary>
        [StringLength(255)]
        public string? PaymentReference { get; set; }

        /// <summary>
        /// Reason provided by the renter for disputing the charge.
        /// </summary>
        [StringLength(1000)]
        public string? DisputeReason { get; set; }

        public DateTime? AuthorizedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public DateTime? ChargedAt { get; set; }
        public DateTime? DisputedAt { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CounterOfferAmount { get; set; }

        [StringLength(500)]
        public string? OwnerDisputeResponse { get; set; }

        public DateTime? CounterOfferAt { get; set; }
        public DateTime? DisputeDeadline { get; set; }
        public DateTime? EscalatedAt { get; set; }

        /// <summary>
        /// Timestamp when the renter explicitly accepted the charge (terminal state).
        /// </summary>
        public DateTime? ChargeAcceptedAt { get; set; }

        /// <summary>
        /// Tracks how many dispute→counter-offer→reject cycles have occurred.
        /// Auto-escalates to admin after reaching the maximum (3).
        /// </summary>
        public int DisputeRoundCount { get; set; }

        [StringLength(500)]
        public string? AdminNotes { get; set; }
        public string? AdminResolvedByUserId { get; set; }
        public DateTime? AdminResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Rental? Rental { get; set; }
        public virtual ICollection<DisputeEvidence> Evidence { get; set; } = new List<DisputeEvidence>();
    }
}
