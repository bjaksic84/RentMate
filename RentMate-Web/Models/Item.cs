// RentMate-Web/Models/Item.cs
using RentMate.Shared;

namespace RentMate.Models
{
    // Ta Item je "nadgradnja" Shared Itema za potrebe baze
    public class Item : RentMate.Shared.Item
    {
        // Ta vrstica zdaj omogoča Entity Frameworku, da poveže tabelo
        public virtual ApplicationUser? User { get; set; }
        
        // Tukaj dodaš še ostale stvari, ki jih rabi samo baza (npr. Rentals, Reviews)
        public virtual List<Rental> Rentals { get; set; } = new();

        public virtual List<Review> Reviews { get; set; } = new();
    }
}