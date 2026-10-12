using System.Globalization;
using MeetSpace.Api.Dtos;

namespace MeetSpace.Api.Bookings;

public static class BookingRequestValidator
{
    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
    ];

    public static Dictionary<string, string[]> Validate(
        BookingRequestDto request, out BookingDetails? booking)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.RoomId is null or <= 0)
        {
            errors["roomId"] = ["Ett befintligt mötesrum måste anges."];
        }

        ValidateText(request.BookedBy, "bookedBy", errors);
        ValidateText(request.Title, "title", errors);
        var startTime = ValidateDateTime(request.StartTime, "startTime", errors);
        var endTime = ValidateDateTime(request.EndTime, "endTime", errors);

        if (startTime.HasValue && endTime.HasValue && endTime <= startTime)
        {
            errors["endTime"] = ["Sluttiden måste vara senare än starttiden."];
        }

        if (request is CreateBookingDto createRequest
            && createRequest.AdditionalProperties?.Keys.Any(
                key => string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)) == true)
        {
            errors["id"] = ["Bokningens ID genereras automatiskt och får inte anges."];
        }

        booking = errors.Count == 0
            ? new BookingDetails(request.RoomId!.Value, request.BookedBy!, request.Title!,
                startTime!.Value, endTime!.Value)
            : null;

        return errors;
    }

    private static void ValidateText(
        string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["Fältet är obligatoriskt."];
            return;
        }

        var messages = new List<string>();

        if (value.Length is < 2 or > 100)
        {
            messages.Add("Fältet måste innehålla mellan 2 och 100 tecken.");
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            messages.Add("Fältet får inte börja eller sluta med whitespace.");
        }

        if (messages.Count > 0)
        {
            errors[field] = messages.ToArray();
        }
    }

    private static DateTime? ValidateDateTime(
        string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["Fältet är obligatoriskt."];
            return null;
        }

        if (!DateTime.TryParseExact(value, DateTimeFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var dateTime))
        {
            errors[field] = ["Fältet måste innehålla ett giltigt datum och klockslag i ISO 8601-format."];
            return null;
        }

        // SQL datetime2 stores no timezone. Normalize explicit offsets to UTC;
        // timestamps without an offset retain the supplied date and time.
        if (dateTime.Kind == DateTimeKind.Local)
        {
            dateTime = dateTime.ToUniversalTime();
        }

        return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
    }
}
