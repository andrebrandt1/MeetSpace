using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MeetSpace.Api.Bookings;

namespace MeetSpace.Api.Tests;

public sealed class UpdateBookingTests
{
    private static readonly Booking Original = new(1, 1, "John Doe", "Veckomöte",
        new DateTime(2026, 10, 15, 9, 0, 0), new DateTime(2026, 10, 15, 10, 0, 0));

    [Fact]
    public async Task Valid_update_replaces_all_fields_and_returns_no_content()
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync("/api/bookings/1", ValidRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new Booking(1, 2, "Jane Doe", "Planering",
            new DateTime(2026, 10, 15, 11, 0, 0), new DateTime(2026, 10, 15, 12, 0, 0)),
            Assert.Single(factory.Repository.Bookings));
    }

    [Fact]
    public async Task Booking_does_not_conflict_with_itself()
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();
        var request = ValidRequest();
        request["roomId"] = 1;
        request["startTime"] = "2026-10-15T09:00:00";
        request["endTime"] = "2026-10-15T10:00:00";

        using var response = await client.PutAsJsonAsync("/api/bookings/1", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Planering", Assert.Single(factory.Repository.Bookings).Title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task Nonexistent_booking_returns_not_found_without_changing_other_bookings(int id)
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync($"/api/bookings/{id}", ValidRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(Original, Assert.Single(factory.Repository.Bookings));
    }

    [Theory]
    [InlineData("roomId")]
    [InlineData("bookedBy")]
    [InlineData("title")]
    [InlineData("startTime")]
    [InlineData("endTime")]
    public async Task Missing_field_returns_bad_request_and_preserves_booking(string field)
    {
        var request = ValidRequest();
        request.Remove(field);
        await AssertInvalidAsync(request, field);
    }

    [Theory]
    [MemberData(nameof(CreateBookingTests.InvalidText), MemberType = typeof(CreateBookingTests))]
    public async Task Invalid_text_returns_bad_request_and_preserves_booking(string field, string? value)
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
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();
        var request = ValidRequest();
        request["bookedBy"] = new string('a', length);
        request["title"] = new string('b', length);

        using var response = await client.PutAsJsonAsync("/api/bookings/1", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new string('a', length), factory.Repository.Bookings[0].BookedBy);
        Assert.Equal(new string('b', length), factory.Repository.Bookings[0].Title);
    }

    [Fact]
    public async Task Whitespace_inside_text_is_allowed_and_preserved()
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();
        var request = ValidRequest();
        request["bookedBy"] = "Jane  Doe";
        request["title"] = "Vecko\tmöte";

        using var response = await client.PutAsJsonAsync("/api/bookings/1", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Jane  Doe", factory.Repository.Bookings[0].BookedBy);
        Assert.Equal("Vecko\tmöte", factory.Repository.Bookings[0].Title);
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
    public async Task Invalid_date_time_returns_bad_request_and_preserves_booking(string field, string? value)
    {
        var request = ValidRequest();
        request[field] = value;
        await AssertInvalidAsync(request, field);
    }

    [Theory]
    [InlineData("2026-10-15T11:00:00")]
    [InlineData("2026-10-15T10:59:59")]
    public async Task End_time_must_be_later_than_start_time(string endTime)
    {
        var request = ValidRequest();
        request["endTime"] = endTime;
        await AssertInvalidAsync(request, "endTime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task Nonexistent_room_returns_bad_request_and_preserves_booking(int roomId)
    {
        var request = ValidRequest();
        request["roomId"] = roomId;
        await AssertInvalidAsync(request, "roomId", repositoryCalls: roomId > 0 ? 1 : 0);
    }

    [Fact]
    public async Task Multiple_validation_errors_are_reported_together_without_updating()
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync("/api/bookings/1", new
        {
            bookedBy = "J",
            title = " Bad title ",
            startTime = "invalid",
            endTime = "invalid"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal(5, errors.EnumerateObject().Count());
        foreach (var field in new[] { "roomId", "bookedBy", "title", "startTime", "endTime" })
        {
            Assert.True(errors.TryGetProperty(field, out _));
        }
        Assert.Equal(Original, Assert.Single(factory.Repository.Bookings));
        Assert.Equal(0, factory.Repository.Calls);
    }

    [Theory]
    [InlineData("2026-10-15T08:30:00", "2026-10-15T09:30:00")]
    [InlineData("2026-10-15T09:30:00", "2026-10-15T10:30:00")]
    [InlineData("2026-10-15T09:15:00", "2026-10-15T09:45:00")]
    [InlineData("2026-10-15T08:00:00", "2026-10-15T11:00:00")]
    [InlineData("2026-10-15T09:00:00", "2026-10-15T10:00:00")]
    public async Task Overlap_in_destination_room_returns_conflict_and_preserves_both_bookings(
        string startTime, string endTime)
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();
        var other = Original with { Id = 2, RoomId = 2 };
        factory.Repository.Bookings.Add(other);
        var request = ValidRequest();
        request["startTime"] = startTime;
        request["endTime"] = endTime;

        using var response = await client.PutAsJsonAsync("/api/bookings/1", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(new[] { Original, other }, factory.Repository.Bookings);
    }

    [Theory]
    [InlineData("2026-10-15T08:00:00", "2026-10-15T09:00:00")]
    [InlineData("2026-10-15T10:00:00", "2026-10-15T11:00:00")]
    public async Task Touching_bookings_in_destination_room_are_allowed(string startTime, string endTime)
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();
        var other = Original with { Id = 2, RoomId = 2 };
        factory.Repository.Bookings.Add(other);
        var request = ValidRequest();
        request["startTime"] = startTime;
        request["endTime"] = endTime;

        using var response = await client.PutAsJsonAsync("/api/bookings/1", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, factory.Repository.Bookings[0].Id);
        Assert.Equal(2, factory.Repository.Bookings[0].RoomId);
        Assert.Equal(other, factory.Repository.Bookings[1]);
    }

    [Fact]
    public async Task Same_time_in_another_room_does_not_conflict()
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();
        factory.Repository.Bookings.Add(Original with { Id = 2, RoomId = 2 });
        var request = ValidRequest();
        request["roomId"] = 1;
        request["startTime"] = "2026-10-15T09:00:00";
        request["endTime"] = "2026-10-15T10:00:00";

        using var response = await client.PutAsJsonAsync("/api/bookings/1", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(2, factory.Repository.Bookings.Count);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"roomId\":\"not-an-integer\"}")]
    [InlineData("{\"startTime\":123}")]
    public async Task Invalid_json_or_body_returns_bad_request_without_updating(string body)
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PutAsync("/api/bookings/1", content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Original, Assert.Single(factory.Repository.Bookings));
        Assert.Equal(0, factory.Repository.Calls);
    }

    private static BookingApiFactory FactoryWithBooking()
    {
        var factory = new BookingApiFactory();
        factory.Repository.Bookings.Add(Original);
        return factory;
    }

    private static Dictionary<string, object?> ValidRequest() => new()
    {
        ["roomId"] = 2,
        ["bookedBy"] = "Jane Doe",
        ["title"] = "Planering",
        ["startTime"] = "2026-10-15T11:00:00",
        ["endTime"] = "2026-10-15T12:00:00"
    };

    private static async Task AssertInvalidAsync(
        Dictionary<string, object?> request, string field, int repositoryCalls = 0)
    {
        await using var factory = FactoryWithBooking();
        using var client = factory.CreateApiClient();

        using var response = await client.PutAsJsonAsync("/api/bookings/1", request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal(Original, Assert.Single(factory.Repository.Bookings));
        Assert.Equal(repositoryCalls, factory.Repository.Calls);
    }
}
