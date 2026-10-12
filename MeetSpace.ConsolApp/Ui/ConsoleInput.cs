using System.Globalization;

namespace MeetSpace.ConsolApp.Ui;

public sealed class ConsoleInput(ConsoleUi ui)
{
    public string Text(string label, int minLength, int maxLength, string? current = null)
    {
        while (true)
        {
            var value = Read(label, current);
            if (value.Length == 0 && current is not null)
            {
                return current;
            }
            if (string.IsNullOrWhiteSpace(value) || value.Length < minLength || value.Length > maxLength)
            {
                ui.Error($"Ange mellan {minLength} och {maxLength} tecken.");
                continue;
            }
            if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
            {
                ui.Error("Texten får inte börja eller sluta med whitespace.");
                continue;
            }
            return value;
        }
    }

    public string? Description(string? current = null, bool updating = false)
    {
        while (true)
        {
            var value = Read(updating ? "Beskrivning (Enter = behåll, - = töm)" : "Beskrivning (valfri)", current);
            if (value.Length > 1000)
            {
                ui.Error("Beskrivningen får ha högst 1000 tecken.");
                continue;
            }
            return value.Length == 0 ? (updating ? current : null) : updating && value == "-" ? null : value;
        }
    }

    public int Number(string label, int minimum = int.MinValue, int? current = null)
    {
        while (true)
        {
            var value = Read(label, current?.ToString(CultureInfo.InvariantCulture));
            if (value.Length == 0 && current.HasValue)
            {
                return current.Value;
            }
            if (int.TryParse(value, out var number) && number >= minimum)
            {
                return number;
            }
            ui.Error(minimum == int.MinValue ? "Ange ett heltal." : $"Ange ett heltal som är minst {minimum}.");
        }
    }

    public DateTime DateTimeValue(string label, DateTime? current = null, DateTime? after = null)
    {
        string[] formats =
        [
            "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF",
            "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"
        ];
        while (true)
        {
            var value = Read($"{label} (åååå-mm-dd HH:mm)", current?.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
            DateTime parsed;
            if (value.Length == 0 && current.HasValue)
            {
                parsed = current.Value;
            }
            else if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                ui.Error("Ange ett giltigt datum och klockslag, t.ex. 2026-10-15 09:00.");
                continue;
            }
            if (after.HasValue && parsed <= after.Value)
            {
                ui.Error("Sluttiden måste vara senare än starttiden.");
                continue;
            }
            return parsed;
        }
    }

    public bool Confirm(string message)
    {
        var value = Read($"{message} (j/n)");
        return value.Equals("j", StringComparison.OrdinalIgnoreCase) || value.Equals("ja", StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string label, string? current = null)
    {
        var display = current is null ? "" : $" [{string.Concat(current.Select(c => char.IsControl(c) ? ' ' : c))}]";
        var prompt = string.Concat(label.Select(c => char.IsControl(c) ? ' ' : c));
        Console.Write($"  {prompt}{display}: ");
        var value = Console.ReadLine() ?? throw new EndOfStreamException();
        if (value.Equals(":avbryt", StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationCanceledException();
        }
        return value;
    }
}
