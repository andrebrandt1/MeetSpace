using MeetSpace.Api.Endpoints;

using MeetSpace.Api.Bookings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IBookingRepository, SqlBookingRepository>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapRoomEndpoints();
app.MapBookingEndpoints();

app.Run();

public partial class Program;
