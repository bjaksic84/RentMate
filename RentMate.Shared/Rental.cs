using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace RentMate.Shared
{
    public enum RentalStatus { Pending, Active, Completed, Cancelled }

    public class Rental
{
    public int Id { get; set; }

    // Item being rented
    public int ItemId { get; set; }
    

    // Person renting (borrowing)
    public string RenterId { get; set; } = string.Empty;
    

    // Person renting out (owner)
    public string? OwnerId { get; set; } = string.Empty;
    

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    public RentalStatus Status { get; set; } = RentalStatus.Pending;

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

}
