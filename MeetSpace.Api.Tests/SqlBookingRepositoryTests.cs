using System.Net;
using System.Net.Http.Json;
using MeetSpace.Api.Bookings;
using MeetSpace.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MeetSpace.Api.Tests;

public sealed class SqlBookingRepositoryTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    public static bool IsSupported => SqlServerFixture.IsSupported;

    private const string SkipReason = "SQL-tester kräver Windows LocalDB eller MEETSPACE_TEST_SQLSERVER.";

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Api_uses_configured_database_and_persists_the_returned_booking()
    {
        await database.ResetAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1,
            bookedBy = "John Doe",
            title = "Veckomöte",
            startTime = "2026-10-15T09:00:00.1234567",
            endTime = "2026-10-15T10:00:00"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(booking);
        Assert.True(booking.Id > 0);
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand("""
            SELECT Id, RoomId, BookedBy, Title, StartTime, EndTime FROM dbo.Bookings;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(booking.Id, reader.GetInt32(0));
        Assert.Equal(booking.RoomId, reader.GetInt32(1));
        Assert.Equal(booking.BookedBy, reader.GetString(2));
        Assert.Equal(booking.Title, reader.GetString(3));
        Assert.Equal(booking.StartTime, reader.GetDateTime(4));
        Assert.Equal(booking.EndTime, reader.GetDateTime(5));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Nonexistent_room_does_not_create_a_booking()
    {
        await database.ResetAsync();
        var result = await Repository().CreateAsync(NewBooking() with { RoomId = 999 }, TestContext.Current.CancellationToken);

        Assert.Equal(CreateBookingStatus.RoomNotFound, result.Status);
        Assert.Null(result.Booking);
        Assert.Equal(0, await database.BookingCountAsync());
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    [InlineData(8, 30, 9, 30)]
    [InlineData(9, 30, 10, 30)]
    [InlineData(9, 15, 9, 45)]
    [InlineData(8, 0, 11, 0)]
    [InlineData(9, 0, 10, 0)]
    public async Task Every_overlap_shape_is_rejected(int startHour, int startMinute, int endHour, int endMinute)
    {
        await database.ResetAsync();
        var repository = Repository();
        var first = await repository.CreateAsync(NewBooking(), TestContext.Current.CancellationToken);
        Assert.Equal(CreateBookingStatus.Created, first.Status);

        var result = await repository.CreateAsync(NewBooking() with
        {
            StartTime = new DateTime(2026, 10, 15, startHour, startMinute, 0),
            EndTime = new DateTime(2026, 10, 15, endHour, endMinute, 0)
        }, TestContext.Current.CancellationToken);

        Assert.Equal(CreateBookingStatus.Conflict, result.Status);
        Assert.Equal(1, await database.BookingCountAsync());
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    [InlineData(8, 9)]
    [InlineData(10, 11)]
    public async Task Adjacent_bookings_are_saved(int startHour, int endHour)
    {
        await database.ResetAsync();
        var repository = Repository();
        var first = await repository.CreateAsync(NewBooking(), TestContext.Current.CancellationToken);
        var second = await repository.CreateAsync(NewBooking() with
        {
            StartTime = new DateTime(2026, 10, 15, startHour, 0, 0),
            EndTime = new DateTime(2026, 10, 15, endHour, 0, 0)
        }, TestContext.Current.CancellationToken);

        Assert.Equal(CreateBookingStatus.Created, first.Status);
        Assert.Equal(CreateBookingStatus.Created, second.Status);
        Assert.NotEqual(first.Booking!.Id, second.Booking!.Id);
        Assert.Equal(2, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Same_time_in_different_rooms_is_saved()
    {
        await database.ResetAsync();
        var repository = Repository();
        var first = await repository.CreateAsync(NewBooking(), TestContext.Current.CancellationToken);
        var second = await repository.CreateAsync(NewBooking() with { RoomId = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(CreateBookingStatus.Created, first.Status);
        Assert.Equal(CreateBookingStatus.Created, second.Status);
        Assert.Equal(2, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Concurrent_api_requests_create_exactly_one_booking_and_return_conflict_for_the_rest()
    {
        await database.ResetAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            client.PostAsJsonAsync("/api/bookings", new
            {
                roomId = 1,
                bookedBy = "John Doe",
                title = "Veckomöte",
                startTime = "2026-10-15T09:00:00",
                endTime = "2026-10-15T10:00:00"
            }, TestContext.Current.CancellationToken)));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Equal(11, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(1, await database.BookingCountAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private SqlBookingRepository Repository() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = database.ConnectionString
        }).Build());

    private static BookingToCreate NewBooking() => new(1, "John Doe", "Veckomöte",
        new DateTime(2026, 10, 15, 9, 0, 0), new DateTime(2026, 10, 15, 10, 0, 0));

    private sealed class SqlBookingApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString }));
        }
    }
}
