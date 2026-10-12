using MeetSpace.Api.Endpoints;
using MeetSpace.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Register a room service
builder.Services.AddScoped<RoomService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapRoomEndpoints();
app.MapBookingListEndpoints();

app.Run();