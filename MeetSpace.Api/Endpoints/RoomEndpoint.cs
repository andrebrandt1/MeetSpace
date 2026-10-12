using MeetSpace.Api.Services;

namespace MeetSpace.Api.Endpoints;

public static class RoomEndpoint
{
	public static void MapRoomEndpoints(
		this WebApplication app)
	{
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
	}
}