using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeetSpace.Api.Bookings;

public sealed class CreateBookingRequest
{
    public int? RoomId { get; init; }
    public string? BookedBy { get; init; }
    public string? Title { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
