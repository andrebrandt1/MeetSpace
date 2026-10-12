using MeetSpace.ConsolApp.Dtos;

namespace MeetSpace.ConsolApp.Ui;

public sealed record MenuOption(int Key, string Label, string? Description = null);

public sealed class ConsoleUi(bool animate = true)
{
    private static readonly ConsoleColor[] Rainbow =
        [ConsoleColor.Red, ConsoleColor.Yellow, ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta];

    private static readonly string[] Logo = BuildLogo();
    private bool Interactive => !Console.IsInputRedirected && !Console.IsOutputRedirected;
    private static int Width => Console.IsOutputRedirected ? 100 : Math.Max(30, Console.WindowWidth);
    private bool LargeLogo => Interactive && Width >= 60 && Console.WindowHeight >= 24;
    private int HeaderHeight => LargeLogo ? 10 : Interactive ? 6 : 3;

    public async Task<int> MenuAsync(string title, IReadOnlyList<MenuOption> options)
    {
        if (!Interactive)
        {
            ShowPage(title);
            foreach (var option in options)
            {
                Console.WriteLine($"  [{option.Key}] {option.Label}");
                if (option.Description is not null)
                {
                    Console.WriteLine($"      {option.Description}");
                }
            }
            while (true)
            {
                Console.Write("  Välj: ");
                var input = Console.ReadLine();
                if (input is null)
                {
                    return 0;
                }
                if (int.TryParse(input, out var choice) && options.Any(option => option.Key == choice))
                {
                    return choice;
                }
                Error("Välj ett av numren i menyn.");
            }
        }

        var selected = 0;
        var frame = 0;
        var previousSize = (Console.WindowWidth, Console.WindowHeight);
        try
        {
            Console.CursorVisible = false;
            DrawMenu(title, options, selected);
            while (true)
            {
                var size = (Console.WindowWidth, Console.WindowHeight);
                if (size != previousSize)
                {
                    previousSize = size;
                    DrawMenu(title, options, selected);
                }
                if (animate)
                {
                    DrawBanner(frame++);
                }
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        return 0;
                    }
                    if (key.Key == ConsoleKey.Enter)
                    {
                        return options[selected].Key;
                    }
                    if (char.IsAsciiDigit(key.KeyChar))
                    {
                        var choice = key.KeyChar - '0';
                        if (options.Any(option => option.Key == choice))
                        {
                            return choice;
                        }
                    }
                    if (key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow)
                    {
                        selected = (selected + (key.Key == ConsoleKey.DownArrow ? 1 : options.Count - 1)) % options.Count;
                        DrawOptions(options, selected);
                    }
                }
                await Task.Delay(80);
            }
        }
        finally
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, Math.Min(Console.WindowHeight - 1,
                HeaderHeight + options.Sum(option => option.Description is null ? 1 : 3) + 3));
        }
    }

    public void ShowPage(string title)
    {
        if (Interactive)
        {
            Console.Clear();
            DrawBanner(0);
            WriteAt(HeaderHeight - 2, title.ToUpperInvariant(), ConsoleColor.Cyan);
            Console.SetCursorPosition(0, HeaderHeight);
        }
        else
        {
            Console.WriteLine($"\n  MeetSpace | {title}\n");
        }
    }

    private void DrawMenu(string title, IReadOnlyList<MenuOption> options, int selected)
    {
        ShowPage(title);
        DrawOptions(options, selected);
    }

    private void DrawOptions(IReadOnlyList<MenuOption> options, int selected)
    {
        var row = HeaderHeight;
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            WriteAt(row++, $"{(index == selected ? "›" : " ")} [{option.Key}] {option.Label}",
                index == selected ? ConsoleColor.Cyan : ConsoleColor.Gray);
            if (option.Description is not null)
            {
                WriteAt(row++, $"      {option.Description}", ConsoleColor.DarkGray);
                row++;
            }
        }
        WriteAt(row + 1, "↑ ↓ välj   Enter öppna   Siffra snabbval   Esc tillbaka", ConsoleColor.DarkGray);
    }

    private void DrawBanner(int frame)
    {
        var lines = LargeLogo ? Logo : ["MeetSpace"];
        for (var row = 0; row < lines.Length; row++)
        {
            Console.SetCursorPosition(2, row + 1);
            var line = lines[row];
            for (var column = 0; column < line.Length; column++)
            {
                Console.ForegroundColor = Rainbow[(column / 3 + frame / 2 + row) % Rainbow.Length];
                Console.Write(line[column]);
            }
        }
        Console.ResetColor();
        WriteAt(lines.Length + 2, "Boka smart. Möt bättre.", ConsoleColor.DarkGray);
    }

    private static void WriteAt(int row, string text, ConsoleColor color)
    {
        if (row >= Console.WindowHeight)
        {
            return;
        }
        Console.SetCursorPosition(2, row);
        Console.ForegroundColor = color;
        Console.Write(Fit(text, Width - 4).PadRight(Width - 4));
        Console.ResetColor();
    }

    public void Success(string message) => WriteLine($"  ✓ {message}", ConsoleColor.Green);
    public void Error(string message) => WriteLine($"  ! {message}", ConsoleColor.Red);
    public void Info(string message) => WriteLine($"  {message}", ConsoleColor.DarkGray);

    private static void WriteLine(string text, ConsoleColor color)
    {
        if (!Console.IsOutputRedirected)
        {
            Console.ForegroundColor = color;
        }
        Console.WriteLine(Clean(text));
        if (!Console.IsOutputRedirected)
        {
            Console.ResetColor();
        }
    }

    public void Rooms(IReadOnlyList<RoomDto> rooms)
    {
        if (rooms.Count == 0)
        {
            Info("Det finns inga mötesrum ännu.");
            return;
        }
        Table(["ID", "NAMN", "PLATSER", "VÅNING"], [5, 32, 9, 7],
            rooms.Select(room => new[] { room.Id.ToString(), room.Name, room.Capacity.ToString(), room.Floor.ToString() }));
    }

    public void Bookings(IReadOnlyList<BookingDto> bookings)
    {
        if (bookings.Count == 0)
        {
            Info("Det finns inga bokningar ännu.");
            return;
        }
        Table(["ID", "RUM", "TITEL", "BOKAD AV", "START", "SLUT"], [4, 4, 14, 12, 16, 16],
            bookings.Select(booking => new[]
            {
                booking.Id.ToString(), booking.RoomId.ToString(), booking.Title, booking.BookedBy,
                booking.StartTime.ToString("yyyy-MM-dd HH:mm"), booking.EndTime.ToString("yyyy-MM-dd HH:mm")
            }));
    }

    public void Room(RoomDto room)
    {
        Console.WriteLine();
        Detail("ID", room.Id);
        Detail("Namn", room.Name);
        Detail("Beskrivning", room.Description ?? "—");
        Detail("Kapacitet", $"{room.Capacity} personer");
        Detail("Våning", room.Floor);
        Console.WriteLine();
    }

    public void Booking(BookingDto booking)
    {
        Console.WriteLine();
        Detail("ID", booking.Id);
        Detail("Rum", booking.RoomId);
        Detail("Titel", booking.Title);
        Detail("Bokad av", booking.BookedBy);
        Detail("Starttid", booking.StartTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF"));
        Detail("Sluttid", booking.EndTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF"));
        Console.WriteLine();
    }

    private static void Detail(string label, object value) =>
        Console.WriteLine($"  {label.PadRight(14)} {Clean(value.ToString() ?? "")}");

    private static void Table(string[] headings, int[] widths, IEnumerable<string[]> rows)
    {
        Console.WriteLine();
        WriteLine("  " + string.Join("  ", headings.Select((heading, index) => Fit(heading, widths[index]).PadRight(widths[index]))), ConsoleColor.Cyan);
        WriteLine("  " + new string('─', widths.Sum() + 2 * (widths.Length - 1)), ConsoleColor.DarkGray);
        foreach (var row in rows)
        {
            Console.WriteLine("  " + string.Join("  ", row.Select((value, index) => Fit(value, widths[index]).PadRight(widths[index]))));
        }
        Console.WriteLine();
    }

    public void Pause()
    {
        Console.WriteLine();
        Info("Tryck Enter för att återgå till menyn.");
        Console.ReadLine();
    }

    private static string Clean(string text) => string.Concat(text.Select(character => char.IsControl(character) ? ' ' : character));
    private static string Fit(string text, int width)
    {
        text = Clean(text);
        return text.Length <= width ? text : text[..Math.Max(0, width - 1)] + "…";
    }

    private static string[] BuildLogo()
    {
        var font = new Dictionary<char, string[]>
        {
            ['M'] = ["█   █", "██ ██", "█ █ █", "█   █", "█   █"],
            ['E'] = ["█████", "█    ", "████ ", "█    ", "█████"],
            ['T'] = ["█████", "  █  ", "  █  ", "  █  ", "  █  "],
            ['S'] = ["█████", "█    ", "█████", "    █", "█████"],
            ['P'] = ["████ ", "█   █", "████ ", "█    ", "█    "],
            ['A'] = [" ███ ", "█   █", "█████", "█   █", "█   █"],
            ['C'] = [" ████", "█    ", "█    ", "█    ", " ████"]
        };
        return Enumerable.Range(0, 5)
            .Select(row => string.Join(" ", "MEETSPACE".Select(letter => font[letter][row]))).ToArray();
    }
}
