namespace MeetSpace.Api.Dto
{
    public class CreateRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Capacity { get; set; }
        public int Floor { get; set; }
    }
}
