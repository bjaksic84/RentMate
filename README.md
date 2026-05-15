# RentMate

Tržnica za izposojo predmetov med uporabniki, izdelana v ASP.NET Core MVC.
Razvito kot seminarska naloga za UL FRI, 2. letnik.

## Realiziran primer uporabe (RIS, 2. del)

Ta koda realizira primer uporabe **Oddaja zahteve za rezervacijo** iz načrta UML
v 1. delu. Širša funkcionalnost RentMate (varščine, spori, ocene, točkovanje,
plačila) ni predmet ocenjevanja seminarske naloge, je pa prisotna v kodi iz
prejšnjih vaj.

## Preslikava VOPC v kodo

| Element načrta (VOPC) | Tip | Realizacija |
|---|---|---|
| `Najemnik` | akter | Prijavljen uporabnik, ki ni lastnik predmeta. Ni ločene sistemske vloge (sistemske vloge so Admin, Moderator, User). |
| `RezervacijskiObrazec` | mejni | `RentMate-Web/Views/Shared/_RentModal.cshtml`, prikazan na strani s podrobnostmi predmeta prek `_ItemBookingCard.cshtml` |
| `RezervacijskiKontroler` | krmilni | `RentMate-Web/Services/Implementations/RezervacijskiKontroler.cs`. Metode `OddajZahtevoAsync`, `ObdelajIzbiroDodatkovAsync`, `IzračunajSkupniZnesek` se preslikajo 1:1 v VOPC. MVC `RentalsController.RequestRental` je tanek HTTP adapter, ki delegira na to storitev. |
| `Uporabnik` | entiteta | `RentMate-Web/Models/Domain/ApplicationUser.cs` (razširja `IdentityUser`). Preslikava atributa: `ocenaZaupanja` v `ProfileTrustScore`. Operacija `pridobiUporabnika` v statično `PridobiUporabnikaAsync`. |
| `Predmet` | entiteta | `RentMate-Web/Models/Domain/Item.cs`. Preslikava: `naziv` v `Title`, `opis` v `Description`, `kategorija` v `Category`, `dnevnaCena` v `Price`, `lokacijaPrevzema` v `Location`. Operacija `pridobiPodatke` v statično `PridobiPodatkeAsync`. |
| `Razpoložljivost` | entiteta | `RentMate-Web/Models/Domain/Razpoložljivost.cs`. Ni shranjena v bazi (izračunana iz vrstic `Rental` prek `CalendarService`); modelirana kot razred zaradi skladnosti z VOPC. Operacija `preveriRazpoložljivost` v statično `PreveriRazpoložljivostAsync`. |
| `Dodatek` | entiteta | `RentMate-Web/Models/Domain/ItemAccessory.cs`. Preslikava: `naziv` v `Name`, `cena` v `DailyPrice`. Operacija `pridobiDodatke` v statično `PridobiDodatkeAsync`. |
| `Rezervacija` | entiteta | `RentMate-Web/Models/Domain/Rental.cs`. Operacija `ustvariRezervacijo` v statično `UstvariRezervacijoAsync`. Status se začne kot `RentalStatus.Pending` (čaka na potrditev). |

## Postavljeni primeri - POMEMBNO

https://rentmate-gdc6decvaqapckcx.polandcentral-01.azurewebsites.net

Za testiranje, sta narejena dva profila. Prvi je najemnik in drugi najemodajalec. Emaila dejansko ne obstajata.

V naprej sva naredila dva emaila: 
renter@gmail.com -> Password-123
owner@gmail.com -> Password-123

KREDITNA KARTIA ZA NAKUPOVANJE: Stripe ima testno številko kartice 4242 4242 4242 4242, ostalo je lahko karkoli.

Postavljeno na Azure App Service, samodejna postavitev iz veje `master` prek
GitHub Actions (`.github/workflows/master_rentmate.yml`).

## Odstopanja od načrta

- **Oddaja v enem koraku**. Prvotni sekvenčni diagrami so prikazovali večkoračni potek (oddaja datumov, prejem seznama dodatkov, izbira dodatkov, ponovna oddaja). Realizacija uporablja enostranski modalni obrazec, kjer se datumi in izbira dodatkov izvedejo skupaj, nato se odda enkrat.

- **`Razpoložljivost` ni shranjena kot tabela**. Razpoložljivost se izpelje iz obstoječih vrstic `Rental` prek `CalendarService.IsDateRangeAvailableAsync`. Razred `Razpoložljivost` obstaja s podpisom metode iz načrta, vendar delegira in ne hrani stanja.
- **`najemnikId` je niz, ne celo število**. Načrt modelira id uporabnika kot `int`. ASP.NET Identity uporablja niz (GUID) kot primarni ključ, zato koda obdrži `string najemnikId`. To je podrobnost predstavitve; logična preslikava se ne spremeni.

## Tehnološki sklad

ASP.NET Core MVC, EF Core, ASP.NET Identity, PostgreSQL (Neon/Azure), SignalR,
Stripe, Cloudinary.

## Ekipa

Rok Studen Levstek (63240305), Bojan Jakšić (63240121). UL FRI, RIS 2025/2026.
