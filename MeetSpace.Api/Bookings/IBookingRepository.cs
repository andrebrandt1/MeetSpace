namespace MeetSpace.Api.Bookings;

public interface IBookingRepository
{
    Task<CreateBookingResult> CreateAsync(
        BookingToCreate booking, CancellationToken cancellationToken);
}

public enum CreateBookingStatus
{
    Created,
    RoomNotFound,
    Conflict
}

public sealed record CreateBookingResult(CreateBookingStatus Status, Booking? Booking = null);
