using System.Text;
using MeetSpace.ConsolApp.Api;
using MeetSpace.ConsolApp.Ui;

namespace MeetSpace.ConsolApp;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);

        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("MeetSpace — rum och bokningar i terminalen");
            Console.WriteLine("Kör: dotnet run --project MeetSpace.ConsolApp -- [--api-url URL] [--no-animation]");
            Console.WriteLine("Standardadress: http://localhost:9000 (kan också anges med MEETSPACE_API_URL).\n");
            Console.WriteLine("1. Rum: hämta alla rum, hämta enskilt rum.");
            Console.WriteLine("2. Mötesrum: skapa, uppdatera och radera mötesrum.");
            Console.WriteLine("3. Bokning: skapa, hämta alla, hämta enskild, uppdatera och radera bokning.");
            return 0;
        }

        var apiUrl = Environment.GetEnvironmentVariable("MEETSPACE_API_URL") ?? "http://localhost:9000";
        var animate = true;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--api-url" when index + 1 < args.Length:
                    apiUrl = args[++index];
                    break;
                case "--no-animation":
                    animate = false;
                    break;
                default:
                    Console.Error.WriteLine("Okänt eller ofullständigt argument. Använd --help för hjälp.");
                    return 2;
            }
        }

        if (!Uri.TryCreate(apiUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
        {
            Console.Error.WriteLine("Ange en giltig HTTP- eller HTTPS-adress med --api-url.");
            return 2;
        }

        using var httpClient = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(15) };
        var application = new MeetSpaceConsoleApplication(new MeetSpaceApiClient(httpClient), new ConsoleUi(animate));
        await application.RunAsync();
        return 0;
    }
}
