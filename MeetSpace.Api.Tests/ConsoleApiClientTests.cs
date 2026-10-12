using System.Net;
using System.Text;
using MeetSpace.ConsolApp.Api;
using MeetSpace.ConsolApp.Dtos;

namespace MeetSpace.Api.Tests;

public sealed class ConsoleApiClientTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    public static bool IsSupported => SqlServerFixture.IsSupported;
    private const string SkipReason = "SQL-tester kräver Windows LocalDB eller MEETSPACE_TEST_SQLSERVER.";

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Console_client_can_create_list_fetch_update_and_delete_a_room()
    {
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var http = factory.CreateApiClient();
        var api = new MeetSpaceApiClient(http);

        var room = await api.CreateRoomAsync(new RoomRequestDto("Aurora", "Whiteboard och skärm", 8, -1));
        Assert.True(room.Id > 0);
        Assert.Equal("Aurora", room.Name);
        Assert.Equal(room, await api.GetRoomAsync(room.Id));
        Assert.Contains(await api.GetRoomsAsync(), item => item.Id == room.Id);

        var invalid = await Assert.ThrowsAsync<ApiException>(() => api.UpdateRoomAsync(room.Id,
            new RoomRequestDto(" Uppdaterat ", "Beskrivning", 0, 2)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(room, await api.GetRoomAsync(room.Id));

        await api.UpdateRoomAsync(room.Id, new RoomRequestDto("Aurora Plus", null, 12, 2));
        Assert.Equal(new RoomDto(room.Id, "Aurora Plus", null, 12, 2), await api.GetRoomAsync(room.Id));
        await api.DeleteRoomAsync(room.Id);
        var missing = await Assert.ThrowsAsync<ApiException>(() => api.GetRoomAsync(room.Id));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.DoesNotContain(await api.GetRoomsAsync(), item => item.Id == room.Id);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Console_client_can_create_list_fetch_update_and_delete_a_booking()
    {
        await database.ResetAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var http = factory.CreateApiClient();
        var api = new MeetSpaceApiClient(http);
        var start = new DateTime(2026, 10, 15, 9, 0, 0);
        var booking = await api.CreateBookingAsync(new BookingRequestDto(1, "John Doe", "Veckomöte", start, start.AddHours(1)));

        Assert.True(booking.Id > 0);
        Assert.Equal(booking, await api.GetBookingAsync(booking.Id));
        Assert.Contains(await api.GetBookingsAsync(), item => item.Id == booking.Id);
        await api.UpdateBookingAsync(booking.Id,
            new BookingRequestDto(2, "Jane Doe", "Planering", start.AddHours(1), start.AddHours(2)));
        Assert.Equal(new BookingDto(booking.Id, 2, "Jane Doe", "Planering", start.AddHours(1), start.AddHours(2)),
            await api.GetBookingAsync(booking.Id));

        await api.DeleteBookingAsync(booking.Id);
        var missing = await Assert.ThrowsAsync<ApiException>(() => api.GetBookingAsync(booking.Id));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(await api.GetBookingsAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Booking_conflict_is_reported_and_preserves_the_original_booking()
    {
        await database.ResetAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var http = factory.CreateApiClient();
        var api = new MeetSpaceApiClient(http);
        var start = new DateTime(2026, 10, 15, 9, 0, 0);
        var request = new BookingRequestDto(1, "John Doe", "Veckomöte", start, start.AddHours(1));
        var original = await api.CreateBookingAsync(request);

        var conflict = await Assert.ThrowsAsync<ApiException>(() => api.CreateBookingAsync(request));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains(conflict.Messages, message => message.Contains("överlappar"));
        Assert.Equal(original, await api.GetBookingAsync(original.Id));
        Assert.Single(await api.GetBookingsAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Room_with_bookings_cannot_be_deleted_until_bookings_are_removed()
    {
        await database.ResetAsync();
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var http = factory.CreateApiClient();
        var api = new MeetSpaceApiClient(http);
        var room = await api.CreateRoomAsync(new RoomRequestDto("Bokat rum", null, 8, 0));
        var start = new DateTime(2026, 10, 15, 9, 0, 0);
        var booking = await api.CreateBookingAsync(new BookingRequestDto(room.Id, "John Doe", "Veckomöte", start, start.AddHours(1)));

        var conflict = await Assert.ThrowsAsync<ApiException>(() => api.DeleteRoomAsync(room.Id));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains(conflict.Messages, message => message.Contains("bokningar"));
        Assert.Equal(room, await api.GetRoomAsync(room.Id));
        Assert.Equal(booking, await api.GetBookingAsync(booking.Id));
        await api.DeleteBookingAsync(booking.Id);
        await api.DeleteRoomAsync(room.Id);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Multiple_validation_errors_are_displayable_in_swedish_without_creating_a_room()
    {
        await using var factory = new SqlBookingApiFactory(database.ConnectionString);
        using var http = factory.CreateApiClient();
        var api = new MeetSpaceApiClient(http);
        var countBefore = (await api.GetRoomsAsync()).Count;

        var invalid = await Assert.ThrowsAsync<ApiException>(() => api.CreateRoomAsync(
            new RoomRequestDto("", new string('a', 1001), 0, 0)));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(3, invalid.Messages.Count);
        Assert.Contains(invalid.Messages, message => message.StartsWith("Namn:"));
        Assert.Contains(invalid.Messages, message => message.StartsWith("Beskrivning:"));
        Assert.Contains(invalid.Messages, message => message.StartsWith("Kapacitet:"));
        Assert.Equal(countBefore, (await api.GetRoomsAsync()).Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "[]")]
    [InlineData(HttpStatusCode.BadRequest, "null")]
    [InlineData(HttpStatusCode.Conflict, "<html>server error</html>")]
    [InlineData(HttpStatusCode.InternalServerError, "Sensitive server stack trace")]
    public async Task Unexpected_error_bodies_produce_a_user_message_instead_of_crashing(HttpStatusCode status, string body)
    {
        using var http = new HttpClient(new ErrorHandler(status, body)) { BaseAddress = new Uri("http://localhost") };
        var api = new MeetSpaceApiClient(http);

        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetRoomsAsync());

        Assert.Equal(status, error.StatusCode);
        Assert.NotEmpty(error.Messages);
        Assert.DoesNotContain("Sensitive", error.Message);
    }

    private sealed class ErrorHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
