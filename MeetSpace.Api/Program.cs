using MeetSpace.Api.Endpoints;
using MeetSpace.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<RoomValidationService>();
builder.Services.AddScoped<RoomService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapRoomEndpoints();

app.Run();