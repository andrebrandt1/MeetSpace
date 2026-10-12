namespace MeetSpace.Api.Dtos;

public record RoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public int Floor { get; set; }
}
