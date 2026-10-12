using MeetSpace.Api.Dto;

namespace MeetSpace.Api.Services;

public class RoomValidationService
{
    public Dictionary<string, string[]> Validate(CreateRoomDto room)
    {
        var errors = new Dictionary<string, string[]>();
        var nameErrors = new List<string>();
        var descriptionErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(room.Name))
        {
            nameErrors.Add("Namn är obligatoriskt.");
        }
        else
        {
            if (room.Name.Length < 2 || room.Name.Length > 50)
            {
                nameErrors.Add("Namn måste vara mellan 2 och 50 tecken.");
            }

            if (room.Name != room.Name.Trim())
            {
                nameErrors.Add("Namn får inte börja eller sluta med whitespace.");
            }
        }

        if (room.Description is not null)
        {
            if (room.Description.Length > 1000)
            {
                descriptionErrors.Add("Beskrivning får vara högst 1000 tecken.");
            }

            if (room.Description != room.Description.Trim())
            {
                descriptionErrors.Add(
                    "Beskrivning får inte börja eller sluta med whitespace.");
            }
        }

        if (nameErrors.Count > 0)
        {
            errors["name"] = nameErrors.ToArray();
        }

        if (descriptionErrors.Count > 0)
        {
            errors["description"] = descriptionErrors.ToArray();
        }

        if (room.Capacity < 1 || room.Capacity > 500)
        {
            errors["capacity"] = ["Kapacitet måste vara mellan 1 och 500."];
        }

        if (room.Floor < -1000 || room.Floor > 1000)
        {
            errors["floor"] = ["Våningsplan måste vara mellan -1000 och 1000."];
        }

        return errors;
    }
}
