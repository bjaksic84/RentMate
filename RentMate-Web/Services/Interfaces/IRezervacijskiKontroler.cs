using RentMate.Models.Domain;

namespace RentMate.Services.Interfaces
{
    /// <summary>
    /// VOPC control class for the "Oddaja zahteve za rezervacijo" use case.
    /// The MVC RentalsController is a thin HTTP adapter that delegates here;
    /// this class owns the reservation submission flow.
    /// </summary>
    public interface IRezervacijskiKontroler
    {
        /// <summary>
        /// Submits a rental request. Maps to VOPC oddajZahtevo(int predmetId, DateTime datumOd, DateTime datumDo).
        /// </summary>
        Task<RezervacijskiRezultat> OddajZahtevoAsync(
            int predmetId,
            DateTime datumOd,
            DateTime datumDo,
            List<int>? izbraniDodatkiIds,
            string najemnikId);

        /// <summary>
        /// Processes the renter's accessory selection. Maps to VOPC obdelajIzbiroDodatkov(Object izbrani).
        /// </summary>
        Task<List<ItemAccessory>> ObdelajIzbiroDodatkovAsync(int predmetId, List<int>? izbraniIds);

        /// <summary>
        /// Computes total price for a rental. Maps to VOPC izračunajSkupniZnesek(decimal dnevnaCena, int stDni, Object izbrani).
        /// </summary>
        decimal IzračunajSkupniZnesek(decimal dnevnaCena, int stDni, List<ItemAccessory> izbrani);
    }

    /// <summary>
    /// Outcome of a reservation request. Carries the new reservation id on success
    /// and a user-facing message (the canonical Slovenian wording on the alternate flow).
    /// </summary>
    public record RezervacijskiRezultat(bool Success, string Message, int? RezervacijaId);
}
