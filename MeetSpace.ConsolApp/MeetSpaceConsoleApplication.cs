using System.Text.Json;
using MeetSpace.ConsolApp.Api;
using MeetSpace.ConsolApp.Dtos;
using MeetSpace.ConsolApp.Ui;

namespace MeetSpace.ConsolApp;

public sealed class MeetSpaceConsoleApplication(MeetSpaceApiClient api, ConsoleUi ui)
{
    private readonly ConsoleInput input = new(ui);

    public async Task RunAsync()
    {
        try
        {
            while (true)
            {
                var choice = await ui.MenuAsync("Huvudmeny",
                [
                    new(1, "Rum", "Hämta alla rum · Hämta enskilt rum"),
                    new(2, "Mötesrum", "Skapa · Uppdatera · Radera mötesrum"),
                    new(3, "Bokning", "Skapa · Hämta alla · Hämta enskild · Uppdatera · Radera"),
                    new(0, "Avsluta")
                ]);

                switch (choice)
                {
                    case 1:
                        await SubmenuAsync("Rum", [new(1, "Hämta alla rum"), new(2, "Hämta enskilt rum"), new(0, "Tillbaka")],
                            action => action == 1 ? ListRoomsAsync() : GetRoomAsync());
                        break;
                    case 2:
                        await SubmenuAsync("Mötesrum",
                            [new(1, "Skapa mötesrum"), new(2, "Uppdatera mötesrum"), new(3, "Radera mötesrum"), new(0, "Tillbaka")],
                            action => action switch { 1 => CreateRoomAsync(), 2 => UpdateRoomAsync(), _ => DeleteRoomAsync() });
                        break;
                    case 3:
                        await SubmenuAsync("Bokning",
                        [
                            new(1, "Skapa bokning"), new(2, "Hämta alla bokningar"), new(3, "Hämta enskild bokning"),
                            new(4, "Uppdatera bokning"), new(5, "Radera bokning"), new(0, "Tillbaka")
                        ], action => action switch
                        {
                            1 => CreateBookingAsync(), 2 => ListBookingsAsync(), 3 => GetBookingAsync(),
                            4 => UpdateBookingAsync(), _ => DeleteBookingAsync()
                        });
                        break;
                    case 0:
                        ui.Info("Tack för idag. Vi ses i MeetSpace!");
                        return;
                }
            }
        }
        catch (EndOfStreamException)
        {
            ui.Info("MeetSpace avslutas.");
        }
    }

    private async Task SubmenuAsync(string title, MenuOption[] options, Func<int, Task> action)
    {
        while (true)
        {
            var choice = await ui.MenuAsync(title, options);
            if (choice == 0)
            {
                return;
            }
            ui.ShowPage(options.Single(option => option.Key == choice).Label);
            try
            {
                await action(choice);
            }
            catch (ApiException exception)
            {
                foreach (var message in exception.Messages)
                {
                    ui.Error(message);
                }
            }
            catch (HttpRequestException)
            {
                ui.Error("Kan inte nå API:t. Starta MeetSpace.Api och kontrollera serveradressen (--api-url).");
            }
            catch (TaskCanceledException)
            {
                ui.Error("Servern svarade inte i tid. Försök igen.");
            }
            catch (OperationCanceledException)
            {
                ui.Info("Åtgärden avbröts.");
            }
            catch (JsonException)
            {
                ui.Error("Servern returnerade ett svar som inte kunde läsas.");
            }
            catch (InvalidDataException exception)
            {
                ui.Error(exception.Message);
            }
            ui.Pause();
        }
    }

    private async Task ListRoomsAsync() => ui.Rooms(await api.GetRoomsAsync());
    private async Task GetRoomAsync() => ui.Room(await api.GetRoomAsync(input.Number("Rummets ID", 1)));
    private async Task ListBookingsAsync() => ui.Bookings(await api.GetBookingsAsync());
    private async Task GetBookingAsync() => ui.Booking(await api.GetBookingAsync(input.Number("Bokningens ID", 1)));

    private async Task CreateRoomAsync()
    {
        ui.Info("Skriv :avbryt i ett fält för att återgå utan att spara.");
        var room = await api.CreateRoomAsync(RoomFields());
        ui.Success($"Mötesrum #{room.Id} skapades.");
        ui.Room(room);
    }

    private async Task UpdateRoomAsync()
    {
        var room = await api.GetRoomAsync(input.Number("Rummets ID", 1));
        ui.Room(room);
        ui.Info("Enter behåller nuvarande värde. Skriv :avbryt för att avbryta.");
        await api.UpdateRoomAsync(room.Id, RoomFields(room));
        ui.Success($"Mötesrum #{room.Id} uppdaterades.");
    }

    private RoomRequestDto RoomFields(RoomDto? current = null) => new(
        input.Text("Namn", 1, 50, current?.Name),
        input.Description(current?.Description, updating: current is not null),
        input.Number("Kapacitet (antal personer)", 1, current?.Capacity),
        input.Number("Våning", current: current?.Floor));

    private async Task DeleteRoomAsync()
    {
        var room = await api.GetRoomAsync(input.Number("Rummets ID", 1));
        ui.Room(room);
        if (!input.Confirm($"Radera mötesrummet '{room.Name}'?"))
        {
            ui.Info("Raderingen avbröts.");
            return;
        }
        await api.DeleteRoomAsync(room.Id);
        ui.Success($"Mötesrum #{room.Id} raderades.");
    }

    private async Task CreateBookingAsync()
    {
        var request = await BookingFieldsAsync();
        if (request is null)
        {
            return;
        }
        var booking = await api.CreateBookingAsync(request);
        ui.Success($"Bokning #{booking.Id} skapades.");
        ui.Booking(booking);
    }

    private async Task UpdateBookingAsync()
    {
        var booking = await api.GetBookingAsync(input.Number("Bokningens ID", 1));
        ui.Booking(booking);
        var request = await BookingFieldsAsync(booking);
        if (request is null)
        {
            return;
        }
        await api.UpdateBookingAsync(booking.Id, request);
        ui.Success($"Bokning #{booking.Id} uppdaterades.");
    }

    private async Task<BookingRequestDto?> BookingFieldsAsync(BookingDto? current = null)
    {
        var rooms = await api.GetRoomsAsync();
        if (rooms.Count == 0)
        {
            ui.Info("Skapa ett mötesrum först via huvudmeny 2 → Skapa mötesrum.");
            return null;
        }
        ui.Rooms(rooms);
        ui.Info(current is null ? "Skriv :avbryt för att avbryta." : "Enter behåller nuvarande värde. Skriv :avbryt för att avbryta.");
        int roomId;
        do
        {
            roomId = input.Number("Mötesrummets ID", 1, current?.RoomId);
            if (rooms.All(room => room.Id != roomId))
            {
                ui.Error("Välj ett av mötesrummen i listan.");
            }
        } while (rooms.All(room => room.Id != roomId));
        var bookedBy = input.Text("Bokad av", 2, 100, current?.BookedBy);
        var title = input.Text("Titel", 2, 100, current?.Title);
        var start = input.DateTimeValue("Starttid", current?.StartTime);
        var end = input.DateTimeValue("Sluttid", current?.EndTime, after: start);
        return new BookingRequestDto(roomId, bookedBy, title, start, end);
    }

    private async Task DeleteBookingAsync()
    {
        var booking = await api.GetBookingAsync(input.Number("Bokningens ID", 1));
        ui.Booking(booking);
        if (!input.Confirm($"Radera bokning #{booking.Id} '{booking.Title}'?"))
        {
            ui.Info("Raderingen avbröts.");
            return;
        }
        await api.DeleteBookingAsync(booking.Id);
        ui.Success($"Bokning #{booking.Id} raderades.");
    }
}
