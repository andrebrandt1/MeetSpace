namespace MeetSpace.Api.Dtos;

public sealed class RoomRequestDto
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public int? Capacity { get; init; }
    public int? Floor { get; init; }
}
