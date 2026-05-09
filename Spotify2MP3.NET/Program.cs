using Spotify2MP3.NET.UI;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

namespace Spotify2MP3.NET;

internal static class Program
{
    private static int Main(string[] args)
    {
        string? defaultFolder = ParseFolderArg(args);

        RegisterSchemes();

        using IApplication app = Application.Create().Init();

        using var mainWindow = new MainWindow(defaultFolder);
        app.Run(mainWindow);

        return 0;
    }

    private static string? ParseFolderArg(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--folder")
                return args[i + 1];
        }
        return null;
    }

    private static void RegisterSchemes()
    {
        SchemeManager.AddScheme(
            "Base",
            new Scheme
            {
                Normal = new Terminal.Gui.Drawing.Attribute(Color.Gray, Color.Black),
                Focus = new Terminal.Gui.Drawing.Attribute(Color.White, Color.DarkGray),
                HotNormal = new Terminal.Gui.Drawing.Attribute(Color.BrightCyan, Color.Black),
                HotFocus = new Terminal.Gui.Drawing.Attribute(Color.BrightCyan, Color.DarkGray),
                Disabled = new Terminal.Gui.Drawing.Attribute(Color.DarkGray, Color.Black),
            }
        );

        SchemeManager.AddScheme(
            "Dialog",
            new Scheme
            {
                Normal = new Terminal.Gui.Drawing.Attribute(Color.White, Color.DarkGray),
                Focus = new Terminal.Gui.Drawing.Attribute(Color.Black, Color.Gray),
                HotNormal = new Terminal.Gui.Drawing.Attribute(Color.BrightCyan, Color.DarkGray),
                HotFocus = new Terminal.Gui.Drawing.Attribute(Color.BrightCyan, Color.Gray),
                Disabled = new Terminal.Gui.Drawing.Attribute(Color.DarkGray, Color.DarkGray),
            }
        );

        SchemeManager.AddScheme(
            "Error",
            new Scheme
            {
                Normal = new Terminal.Gui.Drawing.Attribute(Color.BrightRed, Color.Black),
                Focus = new Terminal.Gui.Drawing.Attribute(Color.White, Color.BrightRed),
                HotNormal = new Terminal.Gui.Drawing.Attribute(Color.BrightYellow, Color.Black),
                HotFocus = new Terminal.Gui.Drawing.Attribute(
                    Color.BrightYellow,
                    Color.BrightRed
                ),
                Disabled = new Terminal.Gui.Drawing.Attribute(Color.DarkGray, Color.Black),
            }
        );
    }
}
