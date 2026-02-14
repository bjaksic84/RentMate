using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentMate.Models.Domain
{
    /// <summary>
    /// Represents photographic or document evidence submitted as part of a dispute.
    /// Can be submitted by either the renter (during dispute/escalation) 
    /// or the owner (during counter-offer/response).
    /// </summary>
    public class DisputeEvidence
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the rental deposit this evidence belongs to.
        /// </summary>
        public int RentalDepositId { get; set; }

        /// <summary>
        /// Secure HTTPS URL of the uploaded evidence (hosted on Cloudinary).
        /// </summary>
        [Required]
        [StringLength(500)]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// ID of the user who uploaded this evidence.
        /// </summary>
        [Required]
        public string SubmittedByUserId { get; set; } = string.Empty;

        /// <summary>
        /// Optional notes or description of what the evidence shows.
        /// </summary>
        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("RentalDepositId")]
        public virtual RentalDeposit? RentalDeposit { get; set; }

        [ForeignKey("SubmittedByUserId")]
        public virtual ApplicationUser? SubmittedBy { get; set; }
    }
}
