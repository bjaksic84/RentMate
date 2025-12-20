using RentMate.Shared;

namespace RentMate.Models
{
    // Ta Item je "nadgradnja" Shared Itema za potrebe baze
    public class Payment : RentMate.Shared.Payment
    {
        // Ta vrstica zdaj omogoča Entity Frameworku, da poveže tabelo
        public virtual Rental? Rental { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }
    
}