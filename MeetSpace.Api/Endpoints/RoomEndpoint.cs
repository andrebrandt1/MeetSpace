using MeetSpace.Api.Dto;
using MeetSpace.Api.Services;
using MeetSpace.Api.Endpoints;

namespace MeetSpace.Api.Endpoints;

public static class RoomEndpoint
{
    public static void MapRoomEndpoints(this WebApplication app)
    {
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
    }
}