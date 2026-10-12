using System.Net.Mail;

namespace MeetSpace.Api.Validator;

public static class EmployeeValidator
{
    public static Dictionary<string, string[]> Validate(string? name, string? email)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateName(name, errors);
        ValidateEmail(email, errors);

        return errors;
    }

    private static void ValidateName(string? name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Fältet är obligatoriskt."];
            return;
        }

        var messages = new List<string>();

        if (name.Length is < 2 or > 100)
        {
            messages.Add("Fältet måste innehålla mellan 2 och 100 tecken.");
        }

        if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1]))
        {
            messages.Add("Fältet får inte börja eller sluta med whitespace.");
        }

        if (messages.Count > 0)
        {
            errors["name"] = messages.ToArray();
        }
    }

    private static void ValidateEmail(string? email, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = ["Fältet är obligatoriskt."];
            return;
        }

        if (email.Length > 254)
        {
            errors["email"] = ["E-postadressen får innehålla högst 254 tecken."];
            return;
        }

        // MailAddress godkänner även "Namn <a@b.se>", därför jämförs adressen exakt.
        if (email.Any(char.IsWhiteSpace)
            || !MailAddress.TryCreate(email, out var address)
            || address.Address != email
            || !address.Host.Contains('.'))
        {
            errors["email"] = ["Fältet måste innehålla en giltig e-postadress."];
        }
    }
}
