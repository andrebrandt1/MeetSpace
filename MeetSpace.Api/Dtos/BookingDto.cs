namespace MeetSpace.Api.Dtos;

public record BookingDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string BookedBy { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
