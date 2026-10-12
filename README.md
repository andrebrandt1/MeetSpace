# MeetSpace

Application for internal meetings

## Köra API:t

API:t använder .NET 10, Dapper och SQL Server. Anslutningssträngarna
`ConnectionStrings:Prod` och `ConnectionStrings:DefaultConnection` ska peka på samma
databas. I utvecklingsmiljön lagras de i projektets
User Secrets, inte i källkoden eller `appsettings.json`.

Exempel för en lokal utvecklingsdatabas (byt server/databas vid behov):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=MeetSpace;Integrated Security=true;Encrypt=false" --project MeetSpace.Api
dotnet user-secrets set "ConnectionStrings:Prod" "Server=(localdb)\MSSQLLocalDB;Database=MeetSpace;Integrated Security=true;Encrypt=false" --project MeetSpace.Api
```

Kör [MeetSpace.sql](MeetSpace.Api/Data/MeetSpace.sql) i SQL Server för att skapa
databasen och tabellerna. SQL-testerna använder samma tabell-definitioner.
Lägg därefter till de mötesrum som ska gå att boka, exempelvis:

```sql
INSERT INTO dbo.Rooms (Name, Capacity, Floor) VALUES (N'Mötesrum 1', 10, 1);
```

Starta API:t i Development-miljön, där User Secrets laddas automatiskt:

```powershell
dotnet run --project MeetSpace.Api --launch-profile https
```

## Konsolappen

Starta API:t och konsolappen i varsin terminal:

```powershell
dotnet run --project MeetSpace.Api --launch-profile http
dotnet run --project MeetSpace.ConsolApp
```

Konsolappen visar en animerad regnbågsrubrik och följande menyer:

| Huvudmeny | Funktioner |
| --- | --- |
| 1. Rum | Hämta alla rum, hämta enskilt rum |
| 2. Mötesrum | Skapa, uppdatera och radera mötesrum |
| 3. Bokning | Skapa, hämta alla, hämta enskild, uppdatera och radera bokning |

Använd siffror eller piltangenter och Enter. `0` eller Esc går tillbaka.
Skriv `:avbryt` i ett formulär för att avbryta. Vid uppdatering behåller Enter
nuvarande värde, och `-` tömmer rummets beskrivning. Radering bekräftas med `j`.
Datum anges exempelvis som `2026-10-15 09:00`.

Standardadressen är `http://localhost:9000`. En annan server kan anges med
`MEETSPACE_API_URL` eller kommandoradsargumentet:

```powershell
dotnet run --project MeetSpace.ConsolApp -- --api-url https://localhost:8000
dotnet run --project MeetSpace.ConsolApp -- --no-animation
```

Animationen körs i en interaktiv terminal. Vid omdirigerad in- eller utmatning
används en vanlig textmeny. `--help` visar samtliga funktioner och startalternativ.

Mötesrum har namn (1–50 tecken), valfri beskrivning (högst 1000 tecken), kapacitet
(minst 1) och våning (heltal, även negativa våningar). Ett rum med bokningar kan
inte raderas förrän bokningarna har raderats.

## Skapa bokning

`POST /api/bookings` med `Content-Type: application/json`:

Requesten använder `CreateBookingDto` och svaret använder `BookingDto` i
`MeetSpace.Api/Dtos`. Endpointen mappar den sparade bokningen till svars-DTO:n.

```json
{
  "roomId": 1,
  "bookedBy": "John Doe",
  "title": "Veckomöte",
  "startTime": "2026-10-15T09:00:00",
  "endTime": "2026-10-15T10:00:00"
}
```

- `201 Created`: den sparade bokningen returneras med ett databasgenererat `id`.
- `400 Bad Request`: valideringsfel eller ett mötesrum som inte finns.
  Valideringsfel returneras i ett `errors`-objekt per fält, med flera fel samtidigt.
- `409 Conflict`: en befintlig bokning i samma rum överlappar tidsintervallet.

`bookedBy` och `title` är obligatoriska, ska innehålla 2–100 tecken och får inte
börja eller sluta med whitespace. Klienten får inte skicka `id`.
Tiderna ska vara giltiga ISO 8601-tidsstämplar med datum och klockslag, och sluttiden
ska vara senare än starttiden. Tidsstämplar utan tidszon sparas som angivna;
uttryckliga tidszoner normaliseras till UTC före jämförelse och lagring.

En bokning får börja exakt när en annan slutar eller sluta exakt när en annan
börjar. Rummets databasrad låses under transaktionen som kontrollerar överlapp och
sparar bokningen, vilket hindrar samtidiga anrop från att dubbelboka samma rum.

Ett färdigt anrop finns i [MeetSpace.Api.http](MeetSpace.Api/MeetSpace.Api.http).

## Uppdatera bokning

`PUT /api/bookings/{id}` använder `UpdateBookingDto` med samma sex fält och
valideringsregler som vid skapande. Bokningens ID hämtas från URL:en.

- `204 No Content`: bokningen har uppdaterats; svaret har ingen body.
- `400 Bad Request`: valideringsfel eller ett mötesrum som inte finns.
- `404 Not Found`: bokningen finns inte.
- `409 Conflict`: tidsintervallet överlappar en annan bokning i det angivna rummet.

Bokningen räknas inte som en konflikt med sig själv. Bokningar som gränsar till
varandra tillåts. Validering sker före sparandet, och databasens kontroll av rum,
överlapp och uppdatering sker i en transaktion. Vid fel lämnas bokningen oförändrad.

## Tester

```powershell
dotnet test --solution MeetSpace.slnx
```

HTTP-testerna kontrollerar validering, statuskoder och tidsgränser.
Konsolklientens API-anrop för samtliga rum- och bokningsfunktioner testas också,
inklusive felmeddelanden och skydd mot att radera rum som har bokningar.
SQL-testerna kontrollerar dessutom sparade data och samtidiga HTTP-anrop mot
den riktiga SQL-implementationen. På Windows används SQL Server LocalDB.
För en annan SQL Server kan `MEETSPACE_TEST_SQLSERVER` anges som anslutningssträng
till en testserver där användaren har rätt att skapa och ta bort databaser.
Varje testkörning skapar en separat databas med ett genererat namn och tar bort
den efteråt. Utan Windows eller denna variabel hoppas SQL-testerna över.
