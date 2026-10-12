using Dapper;
using MeetSpace.Api.Dtos;
using MeetSpace.Api.Models;
using MeetSpace.Api.Services;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Endpoints;

public static class RoomEndpoints
{
	public static WebApplication MapRoomEndpoints(
		this WebApplication app)
	{
		// ============================================
		// GET - Hämta alla mötesrum
		// ============================================

		app.MapGet("/api/rooms", () =>
		{
			using var connection =
				new SqlConnection(
					app.Configuration.GetConnectionString("DefaultConnection")
					?? throw new InvalidOperationException(
						"Anslutningssträngen 'DefaultConnection' saknas."));

			var rooms =
				connection.Query<Room>(
					"""
                    SELECT
                        Id,
                        Name,
                        Description,
                        Capacity,
                        Floor
                    FROM dbo.Rooms
                    ORDER BY Id;
                    """);

			var roomDtos =
				rooms.Select(room => new RoomDto
				{
					Id = room.Id,
					Name = room.Name,
					Description = room.Description,
					Capacity = room.Capacity,
					Floor = room.Floor
				}).ToList();

			return Results.Ok(roomDtos);
		});


		// ============================================
		// GET - Hämta ett specifikt mötesrum
		// ============================================

		app.MapGet("/api/rooms/{id}", async (
			int id,
			RoomService roomService) =>
		{
			var room =
				await roomService.GetRoomByIdAsync(id);

			if (room is null)
			{
				return Results.NotFound();
			}

			return Results.Ok(room);
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


        app.MapDelete("/api/booking/{id:int}", (int id) =>
        {
            using var connection = new SqlConnection(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

            var rows = connection.Execute(
            "DELETE FROM dbo.Rooms WHERE Id = @Id;",
            new { Id = id });

            return rows == 0 ? Results.NotFound() : Results.NoContent();
        });


		// ============================================
		// PUT - Uppdatera mötesrum
		// ============================================

		app.MapPut("/api/rooms/{id}", async (
			int id,
			UpdateRoomDto room,
			RoomService roomService,
			RoomValidationService validationService) =>
		{
			var errors =
				validationService.Validate(room);

			if (errors.Count > 0)
			{
				return Results.ValidationProblem(errors);
			}

			bool updated =
				await roomService.UpdateRoomAsync(
					id,
					room);

			if (!updated)
			{
				return Results.NotFound();
			}

			return Results.NoContent();
		});
		return app;
    }

}
