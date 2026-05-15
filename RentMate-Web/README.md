RENTMATE – PREGLED APLIKACIJE (README)
Bojan Jakšić, 63240121
Rok Studen Levstek, 63240305
RentMate je platforma za izposojo predmetov med uporabniki, zgrajena na ASP.NET Core MVC, Entity Framework Core in ASP.NET Identity. Omogoča uporabnikom, da objavljajo predmete za izposojo, brskajo po objavah, ustvarijo najeme, dodajajo ocene in upravljajo svoj račun. Sistem vključuje iskalnik, upravljanje najemov, sistem ocen in administratorsko ploščo za upravljanje uporabnikov in vsebin.

1. SPLETNA APLIKACIJA (MVC ARHITEKTURA)

RentMate uporablja vzorec Model–View–Controller (MVC).

Modeli:
Glavni poslovni entiteti so ApplicationUser, Item, Rental in Review. Modeli določajo validacijo, strukturo podatkov, relacije in omejitve.

Kontrolerji:
ItemsController, RentalsController, ReviewsController, Account/User kontrolerji, Admin kontrolerji.

Pogledi:
Razor pogledi za iskanje, predmete, najeme, profile in administracijo.

2. PODATKOVNA BAZA (Entity Framework + PostgreSQL)

EF Core + PostgreSQL z glavnimi relacijami:
Uporabnik–Predmeti (cascade), Uporabnik–Najemi (restrict), Predmet–Najemi (cascade), Predmet–Ocene (cascade), Uporabnik–Ocene (cascade).

Podprta integriteta:
Brez prekrivanja najemov, validni razpon ocen, mehko brisanje ocen, avtomatsko posodabljanje statistike ocen.

<img width="2546" height="1368" alt="image" src="https://github.com/user-attachments/assets/a745a018-e1f0-46dd-b651-0125d866f7dd" />


3. AVTENTIKACIJA IN AVTORIZACIJA

ASP.NET Identity:
Registracija, prijava, gesla, potrjevanje e-pošte, ponastavitve, 2FA.

Vloge:
User, Moderator, Admin.

Pravila dostopa:
Urejanje oglasov samo lastnik, ocene samo po zaključku najema, admin dostopa do upravljanja sistema, suspendirani uporabniki nimajo dostopa do objavljanja ali najemov.

4. GLAVNE FUNKCIONALNOSTI

Iskanje in filtriranje, sistem najemov, sistem ocen, upravljanje predmetov, uporabniški profili, administratorska plošča.

5. TEHNOLOGIJE

Backend: ASP.NET Core MVC, C#, Identity, EF Core  
Baza: PostgreSQL (Neon/Azure)  
Frontend: Razor, Bootstrap/Tailwind

6. ZASLONSKE SLIKE PROJEKTA

   Tržnica:
   <img width="975" height="542" alt="image" src="https://github.com/user-attachments/assets/2e1ee036-b74d-4edf-a9ce-4bf55deb6938" />
   
   <img width="318" height="680" alt="image" src="https://github.com/user-attachments/assets/b9196d04-0edf-43ca-9108-51883387db8f" />

   Nadzorna plošča:

   <img width="975" height="577" alt="image" src="https://github.com/user-attachments/assets/64668ab5-8a5d-467c-9a95-53dad79b71e5" />

   <img width="318" height="680" alt="image" src="https://github.com/user-attachments/assets/db26a65d-a109-4683-a481-91b1a69516d0" />

   Podrobnosti predmeta:

   <img width="975" height="567" alt="image" src="https://github.com/user-attachments/assets/8df24c48-7ece-4cb3-93b7-2aea1b67e655" />

   <img width="318" height="680" alt="image" src="https://github.com/user-attachments/assets/1de536cc-4c17-486b-a97c-88b78ee07c81" />



   



   


