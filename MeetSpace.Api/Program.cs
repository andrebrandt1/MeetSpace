using MeetSpace.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapRoomEndpoints();
app.MapBookingListEndpoints();

app.Run();


