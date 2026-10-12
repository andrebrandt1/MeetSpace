using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MeetSpace.Api.Bookings;
using MeetSpace.Api.Dtos;
using Microsoft.Extensions.Configuration;

namespace MeetSpace.Api.Tests;

public sealed class SqlUpdateBookingTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    public static bool IsSupported => SqlServerFixture.IsSupported;
    private const string SkipReason = "SQL-tester kräver Windows LocalDB eller MEETSPACE_TEST_SQLSERVER.";

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Valid_update_persists_all_fields_and_keeps_the_id()
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();
        var request = new UpdateBookingDto
        {
            RoomId = 2,
            BookedBy = "Jane Doe",
            Title = "Planering",
            StartTime = "2026-10-15T11:00:00.1234567",
            EndTime = "2026-10-15T12:00:00"
        };

        using var response = await client.PutAsJsonAsync($"/api/bookings/{original.Id}", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Expected(original.Id, request), await database.GetBookingAsync(original.Id));
        Assert.Equal(1, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Same_room_and_time_do_not_conflict_with_the_booking_itself()
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();
        var request = Request(roomId: 1, startHour: 9, endHour: 10);

        using var response = await client.PutAsJsonAsync($"/api/bookings/{original.Id}", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Expected(original.Id, request), await database.GetBookingAsync(original.Id));
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    [InlineData(1)]
    [InlineData(999)]
    public async Task Nonexistent_booking_returns_not_found_even_if_room_is_missing(int roomId)
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync("/api/bookings/2147483647", Request(roomId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(original, await database.GetBookingAsync(original.Id));
        Assert.Equal(1, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Nonexistent_room_returns_bad_request_and_leaves_all_fields_unchanged()
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync($"/api/bookings/{original.Id}", Request(roomId: 999),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(original, await database.GetBookingAsync(original.Id));
        Assert.Equal(1, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Validation_errors_leave_the_persisted_booking_unchanged()
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync($"/api/bookings/{original.Id}", new UpdateBookingDto
        {
            RoomId = 2,
            BookedBy = " Jane Doe ",
            Title = "P",
            StartTime = "not-a-date",
            EndTime = "not-a-date"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(original, await database.GetBookingAsync(original.Id));
        Assert.Equal(1, await database.BookingCountAsync());
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    [InlineData(8, 30, 9, 30)]
    [InlineData(9, 30, 10, 30)]
    [InlineData(9, 15, 9, 45)]
    [InlineData(8, 0, 11, 0)]
    [InlineData(9, 0, 10, 0)]
    public async Task Overlap_in_new_room_returns_conflict_and_leaves_both_bookings_unchanged(
        int startHour, int startMinute, int endHour, int endMinute)
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        var other = await CreateBookingAsync(roomId: 2);
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync($"/api/bookings/{original.Id}",
            Request(2, startHour, endHour, startMinute, endMinute), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(original, await database.GetBookingAsync(original.Id));
        Assert.Equal(other, await database.GetBookingAsync(other.Id));
        Assert.Equal(2, await database.BookingCountAsync());
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    [InlineData(8, 9)]
    [InlineData(10, 11)]
    public async Task Adjacent_booking_in_new_room_is_allowed(int startHour, int endHour)
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        var other = await CreateBookingAsync(roomId: 2);
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();
        var request = Request(2, startHour, endHour);

        using var response = await client.PutAsJsonAsync($"/api/bookings/{original.Id}", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Expected(original.Id, request), await database.GetBookingAsync(original.Id));
        Assert.Equal(other, await database.GetBookingAsync(other.Id));
        Assert.Equal(2, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Same_time_in_another_room_is_not_a_conflict()
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        var other = await CreateBookingAsync(roomId: 2);
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();
        var request = Request(1, 9, 10);

        using var response = await client.PutAsJsonAsync($"/api/bookings/{original.Id}", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Expected(original.Id, request), await database.GetBookingAsync(original.Id));
        Assert.Equal(other, await database.GetBookingAsync(other.Id));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Concurrent_updates_to_same_room_and_time_allow_one_and_preserve_the_loser()
    {
        await database.ResetAsync();
        var first = await CreateBookingAsync();
        var second = await CreateBookingAsync(roomId: 2);
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();
        var request = Request(roomId: 1);

        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/api/bookings/{first.Id}", request, TestContext.Current.CancellationToken),
            client.PutAsJsonAsync($"/api/bookings/{second.Id}", request, TestContext.Current.CancellationToken));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(firstResponse.StatusCode == HttpStatusCode.NoContent ? Expected(first.Id, request) : first,
            await database.GetBookingAsync(first.Id));
        Assert.Equal(secondResponse.StatusCode == HttpStatusCode.NoContent ? Expected(second.Id, request) : second,
            await database.GetBookingAsync(second.Id));
        Assert.Equal(2, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Concurrent_create_and_update_cannot_double_book_a_room()
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync(roomId: 2);
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();
        var request = Request(roomId: 1);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken),
            client.PutAsJsonAsync($"/api/bookings/{original.Id}", request, TestContext.Current.CancellationToken));
        using var createResponse = responses[0];
        using var updateResponse = responses[1];

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var updated = updateResponse.StatusCode == HttpStatusCode.NoContent;
        Assert.Equal(updated ? HttpStatusCode.Conflict : HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(updated ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, updateResponse.StatusCode);
        Assert.Equal(updated ? Expected(original.Id, request) : original, await database.GetBookingAsync(original.Id));
        Assert.Equal(updated ? 1 : 2, await database.BookingCountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Concurrent_updates_of_same_booking_to_different_rooms_both_complete()
    {
        await database.ResetAsync();
        var original = await CreateBookingAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var client = factory.CreateApiClient();
        var firstRequest = Request(roomId: 1);
        var secondRequest = Request(roomId: 2);

        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/api/bookings/{original.Id}", firstRequest, TestContext.Current.CancellationToken),
            client.PutAsJsonAsync($"/api/bookings/{original.Id}", secondRequest, TestContext.Current.CancellationToken));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        var persisted = await database.GetBookingAsync(original.Id);
        Assert.Contains(persisted, new[] { Expected(original.Id, firstRequest), Expected(original.Id, secondRequest) });
        Assert.Equal(1, await database.BookingCountAsync());
    }

    private async Task<Booking> CreateBookingAsync(int roomId = 1)
    {
        var repository = new SqlBookingRepository(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = database.ConnectionString
            }).Build());
        var result = await repository.CreateAsync(new BookingDetails(roomId, "John Doe", "Veckomöte",
            new DateTime(2026, 10, 15, 9, 0, 0), new DateTime(2026, 10, 15, 10, 0, 0)),
            TestContext.Current.CancellationToken);
        Assert.Equal(CreateBookingStatus.Created, result.Status);
        return Assert.IsType<Booking>(result.Booking);
    }

    private static UpdateBookingDto Request(
        int roomId = 2, int startHour = 11, int endHour = 12, int startMinute = 0, int endMinute = 0) => new()
    {
        RoomId = roomId,
        BookedBy = "Jane Doe",
        Title = "Planering",
        StartTime = new DateTime(2026, 10, 15, startHour, startMinute, 0).ToString("s", CultureInfo.InvariantCulture),
        EndTime = new DateTime(2026, 10, 15, endHour, endMinute, 0).ToString("s", CultureInfo.InvariantCulture)
    };

    private static Booking Expected(int id, UpdateBookingDto request) => new(
        id, request.RoomId!.Value, request.BookedBy!, request.Title!,
        DateTime.Parse(request.StartTime!, CultureInfo.InvariantCulture),
        DateTime.Parse(request.EndTime!, CultureInfo.InvariantCulture));
}
