// RentMate-Web/Models/Item.cs
using RentMate.Shared;

namespace RentMate.Models
{
    // Ta Item je "nadgradnja" Shared Itema za potrebe baze
    public class Rental : RentMate.Shared.Rental
    {
        // Ta vrstica zdaj omogoča Entity Frameworku, da poveže tabelo
        public virtual ApplicationUser? Owner { get; set; }
        
        public virtual ApplicationUser? Renter { get; set; }
        public virtual Item? Item { get; set; }
        
    }
}