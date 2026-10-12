using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Bookings;

public sealed class SqlBookingRepository(IConfiguration configuration) : IBookingRepository
{
    public async Task<CreateBookingResult> CreateAsync(
        BookingDetails booking, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            cancellationToken);

        // Keep the room locked until the overlap check and insert have completed.
        // This also protects a room that does not yet have any bookings.
        var roomCommand = new CommandDefinition(
            "SELECT Id FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = @RoomId;",
            new { booking.RoomId }, transaction, cancellationToken: cancellationToken);

        if (await connection.QuerySingleOrDefaultAsync<int?>(roomCommand) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CreateBookingResult(CreateBookingStatus.RoomNotFound);
        }

        var parameters = CreateParameters(booking);

        var overlapCommand = new CommandDefinition("""
            SELECT TOP (1) Id
            FROM dbo.Bookings
            WHERE RoomId = @RoomId AND StartTime < @EndTime AND EndTime > @StartTime;
            """, parameters, transaction, cancellationToken: cancellationToken);

        if (await connection.QuerySingleOrDefaultAsync<int?>(overlapCommand) is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CreateBookingResult(CreateBookingStatus.Conflict);
        }

        var insertCommand = new CommandDefinition("""
            INSERT INTO dbo.Bookings (RoomId, BookedBy, Title, StartTime, EndTime)
            OUTPUT INSERTED.Id, INSERTED.RoomId, INSERTED.BookedBy, INSERTED.Title,
                   INSERTED.StartTime, INSERTED.EndTime
            VALUES (@RoomId, @BookedBy, @Title, @StartTime, @EndTime);
            """, parameters, transaction, cancellationToken: cancellationToken);

        var created = await connection.QuerySingleAsync<Booking>(insertCommand);
        await transaction.CommitAsync(cancellationToken);

        return new CreateBookingResult(CreateBookingStatus.Created, created);
    }

    public async Task<UpdateBookingStatus> UpdateAsync(
        int id, BookingDetails booking, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            cancellationToken);

        var existingCommand = new CommandDefinition(
            "SELECT Id FROM dbo.Bookings WHERE Id = @Id;",
            new { Id = id }, transaction, cancellationToken: cancellationToken);

        if (await connection.QuerySingleOrDefaultAsync<int?>(existingCommand) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UpdateBookingStatus.BookingNotFound;
        }

        // Use the same destination-room lock as creation. Acquire it before
        // modifying the booking row so both operations follow the same lock order.
        var roomCommand = new CommandDefinition(
            "SELECT Id FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = @RoomId;",
            new { booking.RoomId }, transaction, cancellationToken: cancellationToken);

        if (await connection.QuerySingleOrDefaultAsync<int?>(roomCommand) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UpdateBookingStatus.RoomNotFound;
        }

        var parameters = CreateParameters(booking);
        parameters.Add("Id", id);
        var overlapCommand = new CommandDefinition("""
            SELECT TOP (1) Id
            FROM dbo.Bookings
            WHERE RoomId = @RoomId AND Id <> @Id
              AND StartTime < @EndTime AND EndTime > @StartTime;
            """, parameters, transaction, cancellationToken: cancellationToken);

        if (await connection.QuerySingleOrDefaultAsync<int?>(overlapCommand) is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UpdateBookingStatus.Conflict;
        }

        var updateCommand = new CommandDefinition("""
            UPDATE dbo.Bookings
            SET RoomId = @RoomId, BookedBy = @BookedBy, Title = @Title,
                StartTime = @StartTime, EndTime = @EndTime
            WHERE Id = @Id;
            """, parameters, transaction, cancellationToken: cancellationToken);

        if (await connection.ExecuteAsync(updateCommand) == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UpdateBookingStatus.BookingNotFound;
        }

        await transaction.CommitAsync(cancellationToken);
        return UpdateBookingStatus.Updated;
    }

    private SqlConnection CreateConnection() => new(
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Ange ConnectionStrings:DefaultConnection via User Secrets."));

    private static DynamicParameters CreateParameters(BookingDetails booking)
    {
        var parameters = new DynamicParameters();
        parameters.Add("RoomId", booking.RoomId);
        parameters.Add("BookedBy", booking.BookedBy, DbType.String, size: 100);
        parameters.Add("Title", booking.Title, DbType.String, size: 100);
        parameters.Add("StartTime", booking.StartTime, DbType.DateTime2);
        parameters.Add("EndTime", booking.EndTime, DbType.DateTime2);
        return parameters;
    }
}
