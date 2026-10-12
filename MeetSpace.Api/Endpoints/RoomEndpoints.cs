using Dapper;
using MeetSpace.Api.Dtos;
using MeetSpace.Api.Models;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Endpoints;

public static class RoomEndpoints
{
    public static WebApplication MapRoomEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rooms", () =>
        {
            using var connection = new SqlConnection(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

            var rooms = connection.Query<Room>(
                "SELECT Id, Name, Description, Capacity, Floor FROM dbo.Rooms ORDER BY Id;");

            var roomDtos = rooms.Select(room => new RoomDto
            {
                Id = room.Id,
                Name = room.Name,
                Description = room.Description,
                Capacity = room.Capacity,
                Floor = room.Floor
            }).ToList();

            return Results.Ok(roomDtos);
        });

        app.MapDelete("/api/rooms/{id:int}", (int id) =>
        {
            using var connection = new SqlConnection(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

            var rows = connection.Execute(
            "DELETE FROM dbo.Rooms WHERE Id = @Id;",
            new { Id = id });

            return rows == 0 ? Results.NotFound() : Results.NoContent();
        });

        return app;
    }
}
