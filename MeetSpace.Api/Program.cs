using MeetSpace.Api.Endpoints;
using MeetSpace.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrera RoomService
builder.Services.AddScoped<RoomService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Registrera Room-endpoints
app.MapRoomEndpoints();

app.Run();