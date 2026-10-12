using System.Data;
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

    private static async Task<IResult> GetRoomAsync(
        int id, IConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection(configuration);
        var room = await connection.QuerySingleOrDefaultAsync<RoomDto>(new CommandDefinition("""
            SELECT Id, Name, Description, Capacity, Floor FROM dbo.Rooms WHERE Id = @Id;
            """, new { Id = id }, cancellationToken: cancellationToken));
        return room is null ? Results.NotFound() : Results.Ok(room);
    }

    private static async Task<IResult> CreateRoomAsync(
        RoomRequestDto request, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await using var connection = CreateConnection(configuration);
        var room = await connection.QuerySingleAsync<RoomDto>(new CommandDefinition("""
            INSERT INTO dbo.Rooms (Name, Description, Capacity, Floor)
            OUTPUT INSERTED.Id, INSERTED.Name, INSERTED.Description, INSERTED.Capacity, INSERTED.Floor
            VALUES (@Name, @Description, @Capacity, @Floor);
            """, Parameters(request), cancellationToken: cancellationToken));
        return Results.Created($"/api/rooms/{room.Id}", room);
    }

    private static async Task<IResult> UpdateRoomAsync(
        int id, RoomRequestDto request, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await using var connection = CreateConnection(configuration);
        var parameters = Parameters(request);
        parameters.Add("Id", id);
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.Rooms SET Name = @Name, Description = @Description,
                Capacity = @Capacity, Floor = @Floor WHERE Id = @Id;
            """, parameters, cancellationToken: cancellationToken));
        return rows == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task<IResult> DeleteRoomAsync(
        int id, IConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection(configuration);
        try
        {
            var rows = await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.Rooms WHERE Id = @Id;", new { Id = id },
                cancellationToken: cancellationToken));
            return rows == 0 ? Results.NotFound() : Results.NoContent();
        }
        catch (SqlException exception) when (exception.Number == 547)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Mötesrummet har bokningar. Radera dem innan du raderar rummet.");
        }
    }

    private static SqlConnection CreateConnection(IConfiguration configuration) => new(
        configuration.GetConnectionString("Prod")
        ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

    private static DynamicParameters Parameters(RoomRequestDto request)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Name", request.Name, DbType.String, size: 50);
        parameters.Add("Description", request.Description, DbType.String, size: 1000);
        parameters.Add("Capacity", request.Capacity, DbType.Int32);
        parameters.Add("Floor", request.Floor, DbType.Int32);
        return parameters;
    }

    private static Dictionary<string, string[]> Validate(RoomRequestDto request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Namn är obligatoriskt."];
        }
        else if (request.Name.Length > 50 || char.IsWhiteSpace(request.Name[0]) || char.IsWhiteSpace(request.Name[^1]))
        {
            errors["name"] = ["Namn får ha högst 50 tecken och får inte börja eller sluta med whitespace."];
        }
        if (request.Description?.Length > 1000)
        {
            errors["description"] = ["Beskrivningen får ha högst 1000 tecken."];
        }
        if (request.Capacity is null or < 1)
        {
            errors["capacity"] = ["Kapacitet måste vara minst 1."];
        }
        if (request.Floor is null)
        {
            errors["floor"] = ["Våning är obligatorisk."];
        }
        return errors;
    }
}
