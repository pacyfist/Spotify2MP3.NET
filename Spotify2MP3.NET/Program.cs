using Spotify2MP3.NET.UI;
using Terminal.Gui.App;

namespace Spotify2MP3.NET;

internal static class Program
{
    private static int Main(string[] args)
    {
        string? defaultFolder = ParseFolderArg(args);

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
}
