namespace MeetSpace.Api.Dtos;

public abstract class BookingRequestDto
{
    public int? RoomId { get; init; }
    public string? BookedBy { get; init; }
    public string? Title { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
}
