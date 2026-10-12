using System.Data;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Bookings;

public sealed class SqlBookingRepository(IConfiguration configuration) : IBookingRepository
{
    public async Task<CreateBookingResult> CreateAsync(
        BookingToCreate booking, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Ange ConnectionStrings:DefaultConnection via User Secrets.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            cancellationToken);

        // Keep the room locked until the overlap check and insert have completed.
        // This also protects a room that does not yet have any bookings.
        await using var roomCommand = new SqlCommand(
            "SELECT Id FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = @RoomId;",
            connection, transaction);
        roomCommand.Parameters.Add("@RoomId", SqlDbType.Int).Value = booking.RoomId;

        if (await roomCommand.ExecuteScalarAsync(cancellationToken) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CreateBookingResult(CreateBookingStatus.RoomNotFound);
        }

        await using var overlapCommand = new SqlCommand("""
            SELECT TOP (1) Id
            FROM dbo.Bookings
            WHERE RoomId = @RoomId AND StartTime < @EndTime AND EndTime > @StartTime;
            """, connection, transaction);
        AddTimeParameters(overlapCommand, booking);

        if (await overlapCommand.ExecuteScalarAsync(cancellationToken) is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CreateBookingResult(CreateBookingStatus.Conflict);
        }

        await using var insertCommand = new SqlCommand("""
            INSERT INTO dbo.Bookings (RoomId, BookedBy, Title, StartTime, EndTime)
            OUTPUT INSERTED.Id
            VALUES (@RoomId, @BookedBy, @Title, @StartTime, @EndTime);
            """, connection, transaction);
        AddTimeParameters(insertCommand, booking);
        insertCommand.Parameters.Add("@BookedBy", SqlDbType.NVarChar, 100).Value = booking.BookedBy;
        insertCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 100).Value = booking.Title;

        var id = (int)(await insertCommand.ExecuteScalarAsync(cancellationToken))!;
        await transaction.CommitAsync(cancellationToken);

        return new CreateBookingResult(CreateBookingStatus.Created,
            new Booking(id, booking.RoomId, booking.BookedBy, booking.Title,
                booking.StartTime, booking.EndTime));
    }

    private static void AddTimeParameters(SqlCommand command, BookingToCreate booking)
    {
        command.Parameters.Add("@RoomId", SqlDbType.Int).Value = booking.RoomId;
        command.Parameters.Add("@StartTime", SqlDbType.DateTime2).Value = booking.StartTime;
        command.Parameters.Add("@EndTime", SqlDbType.DateTime2).Value = booking.EndTime;
    }
}
