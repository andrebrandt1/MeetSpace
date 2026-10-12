using MeetSpace.Api.Bookings;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace MeetSpace.Api.Tests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string databaseName = $"MeetSpaceTests_{Guid.NewGuid():N}";
    private string? masterConnectionString;
    private bool databaseCreated;

    public string ConnectionString { get; private set; } = "";

    public static bool IsSupported => OperatingSystem.IsWindows()
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEETSPACE_TEST_SQLSERVER"));

    public async ValueTask InitializeAsync()
    {
        if (!IsSupported)
        {
            return;
        }

        var server = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("MEETSPACE_TEST_SQLSERVER")
            ?? "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Encrypt=false;Connect Timeout=15")
        {
            InitialCatalog = "master"
        };
        masterConnectionString = server.ConnectionString;

        await using var master = new SqlConnection(masterConnectionString);
        await master.OpenAsync();
        var createSql = $"CREATE DATABASE [{databaseName}];";

        if (server.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
        {
            // Keep all LocalDB test data inside the ignored build output directory.
            var dataPath = Path.Combine(AppContext.BaseDirectory, $"{databaseName}.mdf").Replace("'", "''");
            var logPath = Path.Combine(AppContext.BaseDirectory, $"{databaseName}_log.ldf").Replace("'", "''");
            createSql = $"""
                CREATE DATABASE [{databaseName}]
                ON PRIMARY (NAME = N'{databaseName}', FILENAME = N'{dataPath}')
                LOG ON (NAME = N'{databaseName}_log', FILENAME = N'{logPath}');
                """;
        }

        await using var create = new SqlCommand(createSql, master);
        await create.ExecuteNonQueryAsync();
        databaseCreated = true;
        server.InitialCatalog = databaseName;
        ConnectionString = server.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "MeetSpace.sql"));
            const string schemaMarker = "-- Tabellen nedan används också av testerna i deras tillfälliga databas.";
            var schemaStart = script.IndexOf(schemaMarker, StringComparison.Ordinal);
            if (schemaStart < 0)
            {
                throw new InvalidOperationException("Schemasnittet saknas i MeetSpace.sql.");
            }

            var schema = script[(schemaStart + schemaMarker.Length)..];
            foreach (var batch in Regex.Split(schema, @"(?im)^\s*GO\s*$"))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                await using var initialize = new SqlCommand(batch, connection);
                await initialize.ExecuteNonQueryAsync();
            }

            await using var seed = new SqlCommand("""
                INSERT INTO dbo.Rooms (Name, Capacity, Floor)
                VALUES (N'Rum 1', 10, 1), (N'Rum 2', 10, 2);
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("DELETE FROM dbo.Bookings;", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> BookingCountAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Bookings;", connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    public async Task<Booking?> GetBookingAsync(int id)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT Id, RoomId, BookedBy, Title, StartTime, EndTime
            FROM dbo.Bookings WHERE Id = @Id;
            """, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new Booking(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetString(3), reader.GetDateTime(4), reader.GetDateTime(5))
            : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (!databaseCreated)
        {
            return;
        }

        using var pooledConnection = new SqlConnection(ConnectionString);
        SqlConnection.ClearPool(pooledConnection);
        await using var master = new SqlConnection(masterConnectionString);
        await master.OpenAsync();
        // The name is generated by this fixture and never comes from configuration.
        await using var drop = new SqlCommand($"""
            ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{databaseName}];
            """, master);
        await drop.ExecuteNonQueryAsync();
        databaseCreated = false;
    }
}
