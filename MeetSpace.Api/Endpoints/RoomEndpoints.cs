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
					app.Configuration.GetConnectionString("Prod")
					?? throw new InvalidOperationException(
						"Anslutningssträngen 'Prod' saknas."));

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


		return app;
	}
}