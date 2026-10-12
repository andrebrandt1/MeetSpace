# MeetSpace

Application for internal meetings

## Köra API:t

API:t använder .NET 10 och SQL Server. Anslutningssträngen läses från
`ConnectionStrings:DefaultConnection`. I utvecklingsmiljön lagras den i projektets
User Secrets, inte i källkoden eller `appsettings.json`.

Exempel för en lokal utvecklingsdatabas (byt server/databas vid behov):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=MeetSpace;Integrated Security=true;Encrypt=false" --project MeetSpace.Api
```

Skapa databasen `MeetSpace` i SQL Server och kör
[schema.sql](MeetSpace.Api/Database/schema.sql) en gång i den nya databasen.
Lägg därefter till de mötesrum som ska gå att boka, exempelvis:

```sql
INSERT INTO dbo.Rooms (Name) VALUES (N'Mötesrum 1');
```

Starta API:t i Development-miljön, där User Secrets laddas automatiskt:

```powershell
dotnet run --project MeetSpace.Api --launch-profile https
```

## Skapa bokning

`POST /api/bookings` med `Content-Type: application/json`:

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

## Tester

```powershell
dotnet test --solution MeetSpace.slnx
```

HTTP-testerna kontrollerar validering, statuskoder och tidsgränser.
SQL-testerna kontrollerar dessutom sparade data och samtidiga HTTP-anrop mot
den riktiga SQL-implementationen. På Windows används SQL Server LocalDB.
För en annan SQL Server kan `MEETSPACE_TEST_SQLSERVER` anges som anslutningssträng
till en testserver där användaren har rätt att skapa och ta bort databaser.
Varje testkörning skapar en separat databas med ett genererat namn och tar bort
den efteråt. Utan Windows eller denna variabel hoppas SQL-testerna över.
