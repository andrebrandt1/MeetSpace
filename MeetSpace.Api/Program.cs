using MeetSpace.Api.Endpoints;
using MeetSpace.Api.Services;
using MeetSpace.Api.Bookings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IBookingRepository, SqlBookingRepository>();
builder.Services.AddScoped<RoomValidationService>();
builder.Services.AddScoped<RoomService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapRoomEndpoints();
app.MapBookingEndpoints();

app.Run();

