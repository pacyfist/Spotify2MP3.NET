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
        bool headless = HasFlag(args, "--headless");

        if (headless)
        {
            if (source is null)
            {
                Console.Error.WriteLine("error: --headless requires --source");
                return HeadlessRunner.ExitFatal;
            }
            if (folder is null)
            {
                Console.Error.WriteLine("error: --headless requires --folder");
                return HeadlessRunner.ExitFatal;
            }

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

        var mainWindow = new MainWindow(folder, source);
        try
        {
            app.Run(mainWindow);
        }
        finally
        {
            try
            {
                mainWindow.Dispose();
            }
            catch (ArgumentOutOfRangeException)
            {
                // Terminal.Gui 2.1.0: View.Dispose can throw while iterating InternalSubViews on quit.
            }
        }

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

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (var a in args)
        {
            if (a == flag)
                return true;
        }
        return false;
    }
}
