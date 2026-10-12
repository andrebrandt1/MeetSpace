using Dapper;
using MeetSpace.Api.Dtos;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Endpoints;

public static class BookingEndpoints
{
    public static WebApplication MapBookingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/bookings", () =>
        {
            using var connection = new SqlConnection(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

            var bookings = connection.Query<BookingListDto>(
                "SELECT Id, RoomId, BookedBy, Title, StartTime, EndTime FROM dbo.Bookings ORDER BY Id;").ToList();

            return Results.Ok(bookings);
        });

        app.MapGet("/api/bookings/{id:int}", (int id) =>
        {
            using var connection = new SqlConnection(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));
            var booking = connection.QueryFirstOrDefault<BookingListDto>(
                "SELECT Id, RoomId, BookedBy, Title, StartTime, EndTime FROM dbo.Bookings WHERE Id = @Id;",
                new { Id = id });
            if (booking == null)
            {
                return Results.NotFound();
            }
            return Results.Ok(booking);
        });

        app.MapDelete("/api/bookings/{id:int}", (int id) =>
        {
            using var connection = new SqlConnection(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

            var rows = connection.Execute(
                "DELETE FROM dbo.Bookings WHERE Id = @Id;",
                new { Id = id });

            return rows == 0 ? Results.NotFound() : Results.NoContent();
        });

        return app;
    }
}
