namespace MeetSpace.Api.Dtos;

public record UpdateEmployeeDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
