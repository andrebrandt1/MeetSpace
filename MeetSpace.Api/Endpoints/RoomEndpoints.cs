using Dapper;
using MeetSpace.Api.Dtos;
using MeetSpace.Api.Models;
using MeetSpace.Api.Services;
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

        app.MapPost("/api/rooms", async (
           CreateRoomDto room,
           RoomService service,
           RoomValidationService validationService) =>
        {
            var errors = validationService.Validate(room);

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var createdRoom = await service.CreateRoom(room);

            return Results.Created(
                $"/api/rooms/{createdRoom.Id}",
                createdRoom);
        });


        return app;
    }
}
