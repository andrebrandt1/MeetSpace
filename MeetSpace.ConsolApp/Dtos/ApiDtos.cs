namespace MeetSpace.ConsolApp.Dtos;

public sealed record RoomDto(int Id, string Name, string? Description, int Capacity, int Floor);

public sealed record BookingDto(
    int Id, int RoomId, string BookedBy, string Title, DateTime StartTime, DateTime EndTime);

public sealed record RoomRequestDto(string Name, string? Description, int Capacity, int Floor);

public sealed record BookingRequestDto(
    int RoomId, string BookedBy, string Title, DateTime StartTime, DateTime EndTime);
