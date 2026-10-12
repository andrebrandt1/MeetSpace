using Dapper;
using MeetSpace.Api.Dtos;
using MeetSpace.Api.Models;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Services;

public class RoomService
{
	private readonly IConfiguration _configuration;

	public RoomService(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public async Task<RoomDto?> GetRoomByIdAsync(int id)
	{
		string connectionString =
			_configuration.GetConnectionString("DefaultConnection")!;

		await using var connection =
			new SqlConnection(connectionString);

		string sql = """
            SELECT
                Id,
                Name,
                Capacity,
                Floor,
                Description
            FROM dbo.Rooms
            WHERE Id = @Id;
            """;

		var room =
			await connection.QuerySingleOrDefaultAsync<Room>(
				sql,
				new
				{
					Id = id
				});

		if (room is null)
		{
			return null;
		}

		return new RoomDto
		{
			Id = room.Id,
			Name = room.Name,
			Capacity = room.Capacity,
			Floor = room.Floor,
			Description = room.Description
		};
	}
}