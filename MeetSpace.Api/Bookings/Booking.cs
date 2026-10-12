namespace MeetSpace.Api.Bookings;

public sealed record Booking(
    int Id,
    int RoomId,
    string BookedBy,
    string Title,
    DateTime StartTime,
    DateTime EndTime);

public sealed record BookingToCreate(
    int RoomId,
    string BookedBy,
    string Title,
    DateTime StartTime,
    DateTime EndTime);
