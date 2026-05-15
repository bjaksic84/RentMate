using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RentMate.Infrastructure.Data;
using RentMate.Shared.Contracts.Responses;

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
        /// When the rental was archived (hidden from active dashboard tabs).
        /// Null means still visible in active views.
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

        /// <summary>
        /// Creates and persists a new reservation in the Pending state.
        /// Maps to VOPC Rezervacija.ustvariRezervacijo(najemnikId, predmetId, datumOd, datumDo, dodatki, skupniZnesek).
        /// Accessories are attached separately via IAccessoryService after creation.
        /// </summary>
        public static async Task<Rental> UstvariRezervacijoAsync(
            RentMateContext db,
            string najemnikId,
            int predmetId,
            DateTime datumOd,
            DateTime datumDo,
            decimal skupniZnesek)
        {
            var predmet = await db.Items.FindAsync(predmetId);

            var rezervacija = new Rental
            {
                ItemId = predmetId,
                OwnerId = predmet?.UserId ?? string.Empty,
                RenterId = najemnikId,
                StartDate = datumOd,
                EndDate = datumDo,
                Status = RentalStatus.Pending,
                TotalPrice = skupniZnesek
            };

            db.Rentals.Add(rezervacija);
            await db.SaveChangesAsync();
            return rezervacija;
        }
    }
}
