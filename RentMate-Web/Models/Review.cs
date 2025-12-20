using RentMate.Shared;

namespace RentMate.Models
{
    // Ta Item je "nadgradnja" Shared Itema za potrebe baze
    public class Review : RentMate.Shared.Review
    {
        // Ta vrstica zdaj omogoča Entity Frameworku, da poveže tabelo
        public virtual ApplicationUser? Reviewer { get; set; }
        public virtual Item? Item { get; set; }

        public virtual Rental? Rental { get; set; }    
    }
}