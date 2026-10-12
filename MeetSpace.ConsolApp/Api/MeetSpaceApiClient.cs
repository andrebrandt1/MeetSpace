using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MeetSpace.ConsolApp.Dtos;

namespace MeetSpace.ConsolApp.Api;

public sealed class MeetSpaceApiClient(HttpClient client)
{
    public Task<List<RoomDto>> GetRoomsAsync() => GetAsync<List<RoomDto>>("api/rooms");
    public Task<RoomDto> GetRoomAsync(int id) => GetAsync<RoomDto>($"api/rooms/{id}");
    public Task<List<BookingDto>> GetBookingsAsync() => GetAsync<List<BookingDto>>("api/bookings");
    public Task<BookingDto> GetBookingAsync(int id) => GetAsync<BookingDto>($"api/bookings/{id}");

    public async Task<RoomDto> CreateRoomAsync(RoomRequestDto request)
    {
        using var response = await client.PostAsJsonAsync("api/rooms", request);
        await EnsureSuccessAsync(response);
        return await ReadAsync<RoomDto>(response);
    }

    public async Task UpdateRoomAsync(int id, RoomRequestDto request)
    {
        using var response = await client.PutAsJsonAsync($"api/rooms/{id}", request);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteRoomAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/rooms/{id}");
        await EnsureSuccessAsync(response);
    }

    public async Task<BookingDto> CreateBookingAsync(BookingRequestDto request)
    {
        using var response = await client.PostAsJsonAsync("api/bookings", request);
        await EnsureSuccessAsync(response);
        return await ReadAsync<BookingDto>(response);
    }

    public async Task UpdateBookingAsync(int id, BookingRequestDto request)
    {
        using var response = await client.PutAsJsonAsync($"api/bookings/{id}", request);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteBookingAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/bookings/{id}");
        await EnsureSuccessAsync(response);
    }

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await client.GetAsync(path);
        await EnsureSuccessAsync(response);
        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>()
        ?? throw new InvalidDataException("Servern returnerade ett tomt svar.");

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var messages = new List<string>();
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            try
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }
                        foreach (var error in field.Value.EnumerateArray())
                        {
                            if (error.ValueKind == JsonValueKind.String)
                            {
                                messages.Add($"{FieldLabel(field.Name)}: {error.GetString()}");
                            }
                        }
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    messages.Add(title.GetString()!);
                }
            }
            catch (JsonException)
            {
                // A proxy or server may return plain text instead of Problem Details.
            }
        }

        if (messages.Count == 0)
        {
            messages.Add(response.StatusCode switch
            {
                HttpStatusCode.NotFound => "Det angivna rummet eller bokningen finns inte.",
                HttpStatusCode.Conflict => "Åtgärden krockar med befintliga rum eller bokningar.",
                HttpStatusCode.BadRequest => "Kontrollera de angivna uppgifterna.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Du har inte behörighet till denna åtgärd.",
                _ => "Servern kunde inte utföra åtgärden. Försök igen senare."
            });
        }

        throw new ApiException(response.StatusCode, messages);
    }

    private static string FieldLabel(string field) => field switch
    {
        "roomId" => "Mötesrum", "bookedBy" => "Bokad av", "title" => "Titel",
        "startTime" => "Starttid", "endTime" => "Sluttid", "name" => "Namn",
        "description" => "Beskrivning", "capacity" => "Kapacitet", "floor" => "Våning",
        _ => field
    };
}

public sealed class ApiException(HttpStatusCode statusCode, IReadOnlyList<string> messages)
    : Exception(string.Join(Environment.NewLine, messages))
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public IReadOnlyList<string> Messages { get; } = messages;
}
