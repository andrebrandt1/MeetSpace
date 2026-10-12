using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MeetSpace.Api.Dtos;

namespace MeetSpace.Api.Tests;

public sealed class CreateBookingTests
{
    [Fact]
    public async Task Valid_booking_returns_created_with_generated_id_and_all_fields()
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync("/api/bookings", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(booking);
        Assert.True(booking.Id > 0);
        Assert.Equal(1, booking.RoomId);
        Assert.Equal("John Doe", booking.BookedBy);
        Assert.Equal("Veckomöte", booking.Title);
        Assert.Equal(new DateTime(2026, 10, 15, 9, 0, 0), booking.StartTime);
        Assert.Equal(new DateTime(2026, 10, 15, 10, 0, 0), booking.EndTime);
        Assert.Equal($"/api/bookings/{booking.Id}", response.Headers.Location?.OriginalString);
        Assert.Single(factory.Repository.Bookings);
    }

    [Theory]
    [InlineData("roomId")]
    [InlineData("bookedBy")]
    [InlineData("title")]
    [InlineData("startTime")]
    [InlineData("endTime")]
    public async Task Missing_field_returns_bad_request_without_saving(string field)
    {
        var request = ValidRequest();
        request.Remove(field);
        await AssertInvalidAsync(request, field);
    }

    public static TheoryData<string, string?> InvalidText => new()
    {
        { "bookedBy", null }, { "title", null },
        { "bookedBy", "" }, { "title", "" },
        { "bookedBy", " " }, { "title", "  " },
        { "bookedBy", "J" }, { "title", "V" },
        { "bookedBy", new string('a', 101) }, { "title", new string('a', 101) },
        { "bookedBy", " John Doe" }, { "title", " Veckomöte" },
        { "bookedBy", "John Doe " }, { "title", "Veckomöte " },
        { "bookedBy", "\tJohn Doe" }, { "title", "Veckomöte\n" },
        { "bookedBy", "John Doe\u00a0" }, { "title", "\u2003Veckomöte" }
    };

    [Theory]
    [MemberData(nameof(InvalidText))]
    public async Task Invalid_text_returns_bad_request_without_saving(string field, string? value)
    {
        var request = ValidRequest();
        request[field] = value;
        await AssertInvalidAsync(request, field);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public async Task Text_length_boundaries_are_allowed(int length)
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        var request = ValidRequest();
        request["bookedBy"] = new string('a', length);
        request["title"] = new string('b', length);

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Whitespace_inside_text_is_allowed_and_preserved()
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        var request = ValidRequest();
        request["bookedBy"] = "John  Doe";
        request["title"] = "Vecko\tmöte";

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>(TestContext.Current.CancellationToken);
        Assert.Equal("John  Doe", booking!.BookedBy);
        Assert.Equal("Vecko\tmöte", booking.Title);
    }

    [Theory]
    [InlineData("startTime", null)]
    [InlineData("endTime", null)]
    [InlineData("startTime", "")]
    [InlineData("endTime", "not-a-date")]
    [InlineData("startTime", "2026-02-30T09:00:00")]
    [InlineData("endTime", "2026-10-15T24:00:00")]
    [InlineData("startTime", "2026-10-15")]
    [InlineData("endTime", "10:00:00")]
    public async Task Invalid_date_time_returns_bad_request_without_saving(string field, string? value)
    {
        var request = ValidRequest();
        request[field] = value;
        await AssertInvalidAsync(request, field);
    }

    [Theory]
    [InlineData("2026-10-15T09:00:00")]
    [InlineData("2026-10-15T08:59:59")]
    public async Task End_time_must_be_after_start_time(string endTime)
    {
        var request = ValidRequest();
        request["endTime"] = endTime;
        await AssertInvalidAsync(request, "endTime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task Nonexistent_room_returns_bad_request_without_saving(int roomId)
    {
        var request = ValidRequest();
        request["roomId"] = roomId;
        await AssertInvalidAsync(request, "roomId", repositoryCalls: roomId > 0 ? 1 : 0);
    }

    [Fact]
    public async Task Multiple_validation_errors_are_reported_together()
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        var request = new { bookedBy = "J", title = " Bad title ", startTime = "invalid", endTime = "invalid", id = 42 };

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal(6, errors.EnumerateObject().Count());
        foreach (var field in new[] { "roomId", "bookedBy", "title", "startTime", "endTime", "id" })
        {
            Assert.True(errors.TryGetProperty(field, out _));
        }
        Assert.Empty(factory.Repository.Bookings);
        Assert.Equal(0, factory.Repository.Calls);
    }

    [Theory]
    [InlineData("id", 42)]
    [InlineData("ID", 0)]
    [InlineData("Id", null)]
    public async Task Client_cannot_supply_an_id(string property, int? id)
    {
        var request = ValidRequest();
        request[property] = id;
        await AssertInvalidAsync(request, "id");
    }

    [Theory]
    [InlineData("2026-10-15T08:30:00", "2026-10-15T09:30:00")]
    [InlineData("2026-10-15T09:30:00", "2026-10-15T10:30:00")]
    [InlineData("2026-10-15T09:15:00", "2026-10-15T09:45:00")]
    [InlineData("2026-10-15T08:00:00", "2026-10-15T11:00:00")]
    [InlineData("2026-10-15T09:00:00", "2026-10-15T10:00:00")]
    public async Task Overlapping_booking_returns_conflict_without_saving(string startTime, string endTime)
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        using var first = await client.PostAsJsonAsync("/api/bookings", ValidRequest(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var request = ValidRequest();
        request["startTime"] = startTime;
        request["endTime"] = endTime;

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(factory.Repository.Bookings);
    }

    [Theory]
    [InlineData("2026-10-15T08:00:00", "2026-10-15T09:00:00")]
    [InlineData("2026-10-15T10:00:00", "2026-10-15T11:00:00")]
    public async Task Touching_bookings_are_allowed_with_different_generated_ids(string startTime, string endTime)
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        using var first = await client.PostAsJsonAsync("/api/bookings", ValidRequest(), TestContext.Current.CancellationToken);
        var request = ValidRequest();
        request["startTime"] = startTime;
        request["endTime"] = endTime;

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var firstBooking = await first.Content.ReadFromJsonAsync<BookingDto>(TestContext.Current.CancellationToken);
        var secondBooking = await response.Content.ReadFromJsonAsync<BookingDto>(TestContext.Current.CancellationToken);
        Assert.NotEqual(firstBooking!.Id, secondBooking!.Id);
        Assert.Equal(2, factory.Repository.Bookings.Count);
    }

    [Fact]
    public async Task Same_time_in_different_rooms_is_allowed()
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        using var first = await client.PostAsJsonAsync("/api/bookings", ValidRequest(), TestContext.Current.CancellationToken);
        var request = ValidRequest();
        request["roomId"] = 2;

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, factory.Repository.Bookings.Count);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"roomId\":\"not-an-integer\"}")]
    [InlineData("{\"startTime\":123}")]
    public async Task Invalid_json_or_body_returns_bad_request(string body)
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/bookings", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Repository.Bookings);
    }

    [Fact]
    public async Task Fractional_seconds_and_timezone_offsets_are_supported()
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();
        var request = ValidRequest();
        request["startTime"] = "2026-10-15T11:00:00.1234567+02:00";
        request["endTime"] = "2026-10-15T10:00:00Z";

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>(TestContext.Current.CancellationToken);
        Assert.Equal(new DateTime(2026, 10, 15, 9, 0, 0).AddTicks(1234567), booking!.StartTime);
    }

    private static Dictionary<string, object?> ValidRequest() => new()
    {
        ["roomId"] = 1,
        ["bookedBy"] = "John Doe",
        ["title"] = "Veckomöte",
        ["startTime"] = "2026-10-15T09:00:00",
        ["endTime"] = "2026-10-15T10:00:00"
    };

    private static async Task AssertInvalidAsync(
        Dictionary<string, object?> request, string field, int repositoryCalls = 0)
    {
        await using var factory = new BookingApiFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync("/api/bookings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        Assert.Empty(factory.Repository.Bookings);
        Assert.Equal(repositoryCalls, factory.Repository.Calls);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
