using MeetSpace.Api.Bookings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeetSpace.Api.Tests;

public sealed class BookingApiFactory : WebApplicationFactory<Program>
{
    public TestBookingRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBookingRepository>();
            services.AddSingleton<IBookingRepository>(Repository);
        });
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}

public sealed class TestBookingRepository : IBookingRepository
{
    public List<Booking> Bookings { get; } = [];
    public int Calls { get; private set; }

    public Task<CreateBookingResult> CreateAsync(
        BookingToCreate booking, CancellationToken cancellationToken)
    {
        Calls++;

        if (booking.RoomId is not (1 or 2))
        {
            return Task.FromResult(new CreateBookingResult(CreateBookingStatus.RoomNotFound));
        }

        if (Bookings.Any(existing => existing.RoomId == booking.RoomId
            && existing.StartTime < booking.EndTime && existing.EndTime > booking.StartTime))
        {
            return Task.FromResult(new CreateBookingResult(CreateBookingStatus.Conflict));
        }

        var created = new Booking(Bookings.Count + 1, booking.RoomId, booking.BookedBy,
            booking.Title, booking.StartTime, booking.EndTime);
        Bookings.Add(created);
        return Task.FromResult(new CreateBookingResult(CreateBookingStatus.Created, created));
    }
}
