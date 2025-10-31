using System;
using System.ComponentModel.DataAnnotations;

namespace RentMate.Models
{
    public enum RentalStatus { Pending, Active, Completed, Cancelled }

    public class Rental
    {
        public int? Id { get; set; }

        // FK to Item
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        // FK to Renter (User)
        public string? RenterId { get; set; }
        public ApplicationUser? Renter { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public RentalStatus Status { get; set; }
    }
}
