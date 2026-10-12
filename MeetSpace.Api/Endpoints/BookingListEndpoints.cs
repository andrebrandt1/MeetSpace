using Dapper;
using MeetSpace.Api.Dtos;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Endpoints;

public static class BookingListEndpoints
{
    public static WebApplication MapBookingListEndpoints(this WebApplication app)
    {
        app.MapGet("/api/bookings", () =>
        {
            using var connection = new SqlConnection(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

            var bookings = connection.Query<BookingListDto>(
                "SELECT Id, RoomId, BookedBy, Title, StartTime, EndTime FROM dbo.Bookings ORDER BY Id;").ToList();

            return Results.Ok(bookings);
        });

        return app;
    }
}
