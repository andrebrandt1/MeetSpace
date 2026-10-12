using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeetSpace.Api.Dtos;

public sealed class CreateBookingDto : BookingRequestDto
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
