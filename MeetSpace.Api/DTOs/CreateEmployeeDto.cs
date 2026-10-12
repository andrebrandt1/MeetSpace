namespace MeetSpace.Api.Dtos;

public record CreateEmployeeDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
