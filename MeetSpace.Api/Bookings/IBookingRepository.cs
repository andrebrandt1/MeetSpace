namespace MeetSpace.Api.Bookings;

public interface IBookingRepository
{
    Task<CreateBookingResult> CreateAsync(
        BookingDetails booking, CancellationToken cancellationToken);

    Task<UpdateBookingStatus> UpdateAsync(
        int id, BookingDetails booking, CancellationToken cancellationToken);
}

public enum CreateBookingStatus
{
    Created,
    RoomNotFound,
    Conflict
}

public sealed record CreateBookingResult(CreateBookingStatus Status, Booking? Booking = null);

public enum UpdateBookingStatus
{
    Updated,
    BookingNotFound,
    RoomNotFound,
    Conflict
}
