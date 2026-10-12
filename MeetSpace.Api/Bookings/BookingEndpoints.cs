using MeetSpace.Api.Dtos;

namespace MeetSpace.Api.Bookings;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/bookings", CreateAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateBookingDto request,
        IBookingRepository repository,
        CancellationToken cancellationToken)
    {
        var errors = BookingRequestValidator.Validate(request, out var booking);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await repository.CreateAsync(booking!, cancellationToken);

        return result.Status switch
        {
            CreateBookingStatus.Created => CreatedResponse(result.Booking!),
            CreateBookingStatus.RoomNotFound => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["roomId"] = ["Det angivna mötesrummet finns inte."]
                }),
            CreateBookingStatus.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Bokningen överlappar en befintlig bokning för samma mötesrum."),
            _ => throw new InvalidOperationException("Okänt resultat när bokningen skapades.")
        };
    }

    private static IResult CreatedResponse(Booking booking)
    {
        var bookingDto = new BookingDto
        {
            Id = booking.Id,
            RoomId = booking.RoomId,
            BookedBy = booking.BookedBy,
            Title = booking.Title,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime
        };

        return Results.Created($"/api/bookings/{bookingDto.Id}", bookingDto);
    }
}
