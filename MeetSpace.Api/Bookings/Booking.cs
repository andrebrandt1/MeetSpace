namespace MeetSpace.Api.Bookings;

public sealed record Booking(
    int Id,
    int RoomId,
    string BookedBy,
    string Title,
    DateTime StartTime,
    DateTime EndTime);

public sealed record BookingDetails(
    int RoomId,
    string BookedBy,
    string Title,
    DateTime StartTime,
    DateTime EndTime);
