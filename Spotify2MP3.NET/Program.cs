using Spotify2MP3.NET.Core;
using Spotify2MP3.NET.Models;
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

        Config config = Config.Load();
        bool deepSearch = true;

        if (ParseArg(args, "--variants") is { } variants)
        {
            config.Variants = variants
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        int? overrideError = null;
        overrideError ??= TryApplyInt(args, "--duration-min", v => config.DurationMin = v);
        overrideError ??= TryApplyInt(args, "--duration-max", v => config.DurationMax = v);
        overrideError ??= TryApplyBool(args, "--m3u", v => config.GenerateM3u = v);
        overrideError ??= TryApplyBool(args, "--exclude-instrumentals", v => config.ExcludeInstrumentals = v);
        overrideError ??= TryApplyBool(args, "--safe-mode", v => config.SafeMode = v);
        overrideError ??= TryApplyBool(args, "--cover-art", v => config.UseSpotifyCoverArt = v);
        overrideError ??= TryApplyBool(args, "--deep-search", v => deepSearch = v);
        if (overrideError.HasValue)
            return overrideError.Value;

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
                return await HeadlessRunner.RunAsync(source, folder, config, deepSearch, cts.Token);
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        using IApplication app = Application.Create().Init();

        var mainWindow = new MainWindow(config, deepSearch, folder, source);
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

    private static int? TryApplyInt(string[] args, string flag, Action<int> apply)
    {
        string? str = ParseArg(args, flag);
        if (str is null)
            return null;
        if (!int.TryParse(str, out int v))
        {
            Console.Error.WriteLine($"error: {flag} must be an integer (got '{str}')");
            return HeadlessRunner.ExitFatal;
        }
        apply(v);
        return null;
    }

    private static int? TryApplyBool(string[] args, string flag, Action<bool> apply)
    {
        string? str = ParseArg(args, flag);
        if (str is null)
            return null;
        if (str.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            apply(true);
            return null;
        }
        if (str.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            apply(false);
            return null;
        }
        Console.Error.WriteLine($"error: {flag} must be 'true' or 'false' (got '{str}')");
        return HeadlessRunner.ExitFatal;
    }
}
