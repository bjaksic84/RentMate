RENTMATE – PREGLED APLIKACIJE (README)

RentMate je platforma za izposojo predmetov med uporabniki, zgrajena na ASP.NET Core MVC, Entity Framework Core in ASP.NET Identity. Omogoča uporabnikom, da objavljajo predmete za izposojo, brskajo po objavah, ustvarijo najeme, dodajajo ocene in upravljajo svoj račun. Sistem vključuje iskalnik, upravljanje najemov, sistem ocen in administratorsko ploščo za upravljanje uporabnikov in vsebin.

1. SPLETNA APLIKACIJA (MVC ARHITEKTURA)

RentMate uporablja vzorec Model–View–Controller (MVC).

Modeli:
Glavni poslovni entiteti so ApplicationUser, Item, Rental in Review. Modeli določajo validacijo, strukturo podatkov, relacije in omejitve.

Kontrolerji:
ItemsController, RentalsController, ReviewsController, Account/User kontrolerji, Admin kontrolerji.

Pogledi:
Razor pogledi za iskanje, predmete, najeme, profile in administracijo.

2. PODATKOVNA BAZA (Entity Framework + SQL Server)

EF Core + SQL Server z glavnimi relacijami:
Uporabnik–Predmeti (cascade), Uporabnik–Najemi (restrict), Predmet–Najemi (cascade), Predmet–Ocene (cascade), Uporabnik–Ocene (restrict).

Podprta integriteta:
Brez prekrivanja najemov, validni razpon ocen, mehko brisanje ocen, avtomatsko posodabljanje statistike ocen.

3. AVTENTIKACIJA IN AVTORIZACIJA

ASP.NET Identity:
Registracija, prijava, gesla, potrjevanje e-pošte, ponastavitve, 2FA.

Vloge:
User, Owner, Admin, SuperAdmin.

Pravila dostopa:
Urejanje oglasov samo lastnik, ocene samo po zaključku najema, admin dostopa do upravljanja sistema, suspendirani uporabniki nimajo dostopa do objavljanja ali najemov.

4. GLAVNE FUNKCIONALNOSTI

Iskanje in filtriranje, sistem najemov, sistem ocen, upravljanje predmetov, uporabniški profili, administratorska plošča.

5. TEHNOLOGIJE

Backend: ASP.NET Core MVC, C# 12, Identity, EF Core  
Baza: SQL Server  
Frontend: Razor, Bootstrap/Tailwind

6. NAMESTITEV

dotnet restore  
dotnet ef database update  
dotnet run

Aplikacija teče na http://localhost:5000 in https://localhost:5001

7. LICENCA

Projekt je interni in lastniški.

