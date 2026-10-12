using MeetSpace.Api.Endpoints;
using MeetSpace.Api.Services;

using MeetSpace.Api.Bookings;

var builder = WebApplication.CreateBuilder(args);

// Register a room service
builder.Services.AddScoped<RoomService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapRoomEndpoints();
app.MapBookingEndpoints();
app.MapBookingWriteEndpoints();
app.MapEmployeeEndpoints();

app.Run();