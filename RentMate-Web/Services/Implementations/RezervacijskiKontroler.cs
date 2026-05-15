using Microsoft.AspNetCore.Identity;
using RentMate.Infrastructure.Data;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations
{
    /// <summary>
    /// VOPC control class for the "Oddaja zahteve za rezervacijo" use case.
    /// Thin facade over the existing reservation logic: it orchestrates the
    /// entity classes (Uporabnik, Predmet, Razpoložljivost, Dodatek, Rezervacija)
    /// so the design maps 1:1 to code. It does not re-implement domain rules.
    /// </summary>
    public sealed class RezervacijskiKontroler : IRezervacijskiKontroler
    {
        private const string SporociloNerazpolozljiv = "Predmet ni razpoložljiv";
        private const string SporociloPredmetNiNajden = "Predmet ni najden.";
        private const string SporociloUspeh = "Zahteva za rezervacijo je bila oddana.";

        private readonly RentMateContext _context;
        private readonly IAccessoryService _accessoryService;
        private readonly ICalendarService _calendarService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RezervacijskiKontroler> _logger;

        public RezervacijskiKontroler(
            RentMateContext context,
            IAccessoryService accessoryService,
            ICalendarService calendarService,
            UserManager<ApplicationUser> userManager,
            ILogger<RezervacijskiKontroler> logger)
        {
            _context = context;
            _accessoryService = accessoryService;
            _calendarService = calendarService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<RezervacijskiRezultat> OddajZahtevoAsync(
            int predmetId,
            DateTime datumOd,
            DateTime datumDo,
            List<int>? izbraniDodatkiIds,
            string najemnikId)
        {
            // Uporabnik.pridobiUporabnika
            var najemnik = await ApplicationUser.PridobiUporabnikaAsync(_userManager, najemnikId);
            if (najemnik == null)
            {
                return new RezervacijskiRezultat(false, SporociloPredmetNiNajden, null);
            }

            // Predmet.pridobiPodatke
            var predmet = await Item.PridobiPodatkeAsync(_context, predmetId);
            if (predmet == null)
            {
                return new RezervacijskiRezultat(false, SporociloPredmetNiNajden, null);
            }

            // Razpoložljivost.preveriRazpoložljivost
            var naVoljo = await Razpoložljivost.PreveriRazpoložljivostAsync(
                _calendarService, predmetId, datumOd, datumDo);
            if (!naVoljo)
            {
                _logger.LogInformation(
                    "Reservation rejected: item {ItemId} unavailable for {From}-{To}",
                    predmetId, datumOd, datumDo);
                return new RezervacijskiRezultat(false, SporociloNerazpolozljiv, null);
            }

            // Dodatek selection
            var izbrani = await ObdelajIzbiroDodatkovAsync(predmetId, izbraniDodatkiIds);

            // izračunajSkupniZnesek
            var stDni = Math.Max((datumDo.Date - datumOd.Date).Days + 1, 1);
            var skupniZnesek = IzračunajSkupniZnesek(predmet.Price ?? 0, stDni, izbrani);

            // Rezervacija.ustvariRezervacijo
            var rezervacija = await Rental.UstvariRezervacijoAsync(
                _context, najemnikId, predmetId, datumOd, datumDo, skupniZnesek);

            // Snapshot accessories onto the reservation (price already included above).
            if (izbrani.Count > 0)
            {
                await _accessoryService.AttachAccessoriesToRentalAsync(
                    rezervacija.Id, izbrani.Select(a => a.Id).ToList());
            }

            return new RezervacijskiRezultat(true, SporociloUspeh, rezervacija.Id);
        }

        /// <inheritdoc />
        public async Task<List<ItemAccessory>> ObdelajIzbiroDodatkovAsync(int predmetId, List<int>? izbraniIds)
        {
            var vsiDodatki = await ItemAccessory.PridobiDodatkeAsync(_accessoryService, predmetId);

            if (izbraniIds == null || izbraniIds.Count == 0)
            {
                return new List<ItemAccessory>();
            }

            return vsiDodatki
                .Where(a => izbraniIds.Contains(a.Id) && a.IsAvailable)
                .ToList();
        }

        /// <inheritdoc />
        public decimal IzračunajSkupniZnesek(decimal dnevnaCena, int stDni, List<ItemAccessory> izbrani)
        {
            var osnova = dnevnaCena * stDni;
            var dodatki = izbrani.Sum(a => a.DailyPrice * stDni);
            return osnova + dodatki;
        }
    }
}
