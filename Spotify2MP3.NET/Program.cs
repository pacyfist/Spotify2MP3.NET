using Spotify2MP3.NET.Core;
using Spotify2MP3.NET.UI;
using Terminal.Gui.App;

namespace Spotify2MP3.NET;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? folder = ParseArg(args, "--folder");
        string? source = ParseArg(args, "--source");

        if (folder is not null && source is not null)
        {
            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            Console.CancelKeyPress += handler;
            try
            {
                return await HeadlessRunner.RunAsync(source, folder, deepSearch: true, cts.Token);
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        using IApplication app = Application.Create().Init();

        using var mainWindow = new MainWindow(folder, source);
        app.Run(mainWindow);

        return 0;
    }

    private static string? ParseArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return null;
    }
}
