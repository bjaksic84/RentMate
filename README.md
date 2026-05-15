# RentMate

Tržnica za izposojo predmetov med uporabniki, izdelana v ASP.NET Core MVC.
Razvito kot seminarska naloga pri predmetu RIS (UL FRI), 2. del.

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

## Odstopanja od načrta

- **Oddaja v enem koraku**. Prvotni sekvenčni diagrami so prikazovali večkoračni potek (oddaja datumov, prejem seznama dodatkov, izbira dodatkov, ponovna oddaja). Realizacija uporablja enostranski modalni obrazec, kjer se datumi in izbira dodatkov izvedejo skupaj, nato se odda enkrat. Sekvenčni diagrami v `docs/uml/` odražajo dejanski potek.
- **`Razpoložljivost` ni shranjena kot tabela**. Razpoložljivost se izpelje iz obstoječih vrstic `Rental` prek `CalendarService.IsDateRangeAvailableAsync`. Razred `Razpoložljivost` obstaja s podpisom metode iz načrta, vendar delegira in ne hrani stanja.
- **`najemnikId` je niz, ne celo število**. Načrt modelira id uporabnika kot `int`. ASP.NET Identity uporablja niz (GUID) kot primarni ključ, zato koda obdrži `string najemnikId`. To je podrobnost predstavitve; logična preslikava se ne spremeni.

## Lokalni zagon

Zahteve: .NET SDK za `net10.0`, PostgreSQL.

Skrivnosti se upravljajo prek `dotnet user-secrets` (id projekta
`dd670d54-9392-4ce5-84de-d962109329e0`):

```bash
cd RentMate-Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<povezovalni niz za postgres>"
dotnet user-secrets set "Jwt:Key" "<ključ za podpisovanje JWT>"
dotnet user-secrets set "AdminUser:Password" "<geslo administratorja>"
# Ključi Cloudinary so potrebni le za nalaganje slik, ne za potek rezervacije.
```

Uvedba migracij in zagon:

```bash
dotnet ef database update --project RentMate-Web/RentMate.csproj
dotnet run --project RentMate-Web/RentMate.csproj
```

Aplikacija ob zagonu samodejno uvede morebitne čakajoče migracije, zato je korak
`dotnet ef database update` neobvezen (zahteva globalno orodje `dotnet-ef`).

Razvojni naslovi: `https://localhost:7280` / `http://localhost:5276`. Swagger na
`/swagger` v okolju Development.

Ob prvem zagonu `DataSeeder` ustvari tudi vzorčni objavljen predmet z dvema
dodatkoma, da je primer uporabe izvedljiv. Registrirajte račun, odprite objavljen
predmet, izberite datume in dodatke ter oddajte. Datumi, ki se prekrivajo z obstoječo
rezervacijo, sprožijo alternativni tok s sporočilom `Predmet ni razpoložljiv`.
Postavljeni primerek že vsebuje objavljene predmete, zato je potek mogoče preizkusiti
tudi neposredno tam.

## Postavljeni primerek

https://rentmate-gdc6decvaqapckcx.polandcentral-01.azurewebsites.net

Postavljeno na Azure App Service, samodejna postavitev iz veje `master` prek
GitHub Actions (`.github/workflows/master_rentmate.yml`).

## Tehnološki sklad

ASP.NET Core MVC, EF Core, ASP.NET Identity, PostgreSQL (Neon/Azure), SignalR,
Stripe, Cloudinary.

## Ekipa

Rok Studen Levstek (63240305), Bojan Jakšić (63240121). UL FRI, RIS 2025/2026.
