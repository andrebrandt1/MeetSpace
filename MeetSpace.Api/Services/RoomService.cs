using Dapper;
using MeetSpace.Api.Dto;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Services;

public class RoomService
{
    private readonly string _connectionString;

    public RoomService(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("MeetSpace")
            ?? throw new InvalidOperationException(
                "Connection string 'MeetSpace' saknas.");
    }

    public async Task<RoomDto> CreateRoom(CreateRoomDto room)
    {
        using var connection = new SqlConnection(_connectionString);

        const string sql = """
            INSERT INTO dbo.Rooms (Name, Description, Capacity, Floor)
            OUTPUT INSERTED.Id,
                   INSERTED.Name,
                   INSERTED.Description,
                   INSERTED.Capacity,
                   INSERTED.Floor
            VALUES (@Name, @Description, @Capacity, @Floor);
            """;

        return await connection.QuerySingleAsync<RoomDto>(sql, new
        {
            room.Name,
            room.Description,
            room.Capacity,
            room.Floor
        });
    }
}