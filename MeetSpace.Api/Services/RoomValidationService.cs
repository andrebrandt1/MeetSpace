using MeetSpace.Api.Dtos;

namespace MeetSpace.Api.Services;

public class RoomValidationService
{
	public Dictionary<string, string[]> Validate(
		CreateRoomDto room)
	{
		return ValidateRoom(
			room.Name,
			room.Description,
			room.Capacity,
			room.Floor);
	}


	public Dictionary<string, string[]> Validate(
		UpdateRoomDto room)
	{
		return ValidateRoom(
			room.Name,
			room.Description,
			room.Capacity,
			room.Floor);
	}


	private Dictionary<string, string[]> ValidateRoom(
		string name,
		string? description,
		int capacity,
		int floor)
	{
		var errors =
			new Dictionary<string, string[]>();

		var nameErrors =
			new List<string>();

		var descriptionErrors =
			new List<string>();


		// ============================================
		// Name
		// ============================================

		if (string.IsNullOrWhiteSpace(name))
		{
			nameErrors.Add(
				"Namn är obligatoriskt.");
		}
		else
		{
			if (name.Length < 2 ||
				name.Length > 50)
			{
				nameErrors.Add(
					"Namn måste vara mellan 2 och 50 tecken.");
			}

			if (name != name.Trim())
			{
				nameErrors.Add(
					"Namn får inte börja eller sluta med whitespace.");
			}
		}


		// ============================================
		// Description
		// ============================================

		if (description is not null)
		{
			if (description.Length > 1000)
			{
				descriptionErrors.Add(
					"Beskrivning får vara högst 1000 tecken.");
			}

			if (description != description.Trim())
			{
				descriptionErrors.Add(
					"Beskrivning får inte börja eller sluta med whitespace.");
			}
		}


		if (nameErrors.Count > 0)
		{
			errors["name"] =
				nameErrors.ToArray();
		}

		if (descriptionErrors.Count > 0)
		{
			errors["description"] =
				descriptionErrors.ToArray();
		}


		// ============================================
		// Capacity
		// ============================================

		if (capacity < 1 ||
			capacity > 500)
		{
			errors["capacity"] =
				["Kapacitet måste vara mellan 1 och 500."];
		}


		// ============================================
		// Floor
		// ============================================

		if (floor < -1000 ||
			floor > 1000)
		{
			errors["floor"] =
				["Våningsplan måste vara mellan -1000 och 1000."];
		}


		return errors;
	}
}