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


	// ============================================
	// Hämta ett specifikt mötesrum
	// ============================================

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


	// ============================================
	// Skapa nytt mötesrum
	// ============================================

	public async Task<RoomDto> CreateRoom(
		CreateRoomDto room)
	{
		string connectionString =
			_configuration.GetConnectionString("DefaultConnection")!;

		await using var connection =
			new SqlConnection(connectionString);

		string sql = """
            INSERT INTO dbo.Rooms
            (
                Name,
                Description,
                Capacity,
                Floor
            )
            OUTPUT
                INSERTED.Id,
                INSERTED.Name,
                INSERTED.Description,
                INSERTED.Capacity,
                INSERTED.Floor
            VALUES
            (
                @Name,
                @Description,
                @Capacity,
                @Floor
            );
            """;

		var createdRoom =
			await connection.QuerySingleAsync<Room>(
				sql,
				new
				{
					Name = room.Name,
					Description = room.Description,
					Capacity = room.Capacity,
					Floor = room.Floor
				});

		return new RoomDto
		{
			Id = createdRoom.Id,
			Name = createdRoom.Name,
			Description = createdRoom.Description,
			Capacity = createdRoom.Capacity,
			Floor = createdRoom.Floor
		};
	}


	// ============================================
	// Uppdatera mötesrum
	// ============================================

	public async Task<bool> UpdateRoomAsync(
		int id,
		UpdateRoomDto room)
	{
		string connectionString =
			_configuration.GetConnectionString("DefaultConnection")!;

		await using var connection =
			new SqlConnection(connectionString);

		string sql = """
            UPDATE dbo.Rooms
            SET
                Name = @Name,
                Description = @Description,
                Capacity = @Capacity,
                Floor = @Floor
            WHERE Id = @Id;
            """;

		int rowsAffected =
			await connection.ExecuteAsync(
				sql,
				new
				{
					Id = id,
					Name = room.Name,
					Description = room.Description,
					Capacity = room.Capacity,
					Floor = room.Floor
				});

		return rowsAffected > 0;
	}
}