namespace MeetSpace.Api.Dtos;

public class UpdateRoomDto
{
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public int Capacity { get; set; }

	public int Floor { get; set; }
}