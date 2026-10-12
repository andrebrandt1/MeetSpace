namespace MeetSpace.Api.Dtos;

public sealed record BookingDto
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public string BookedBy { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
}
