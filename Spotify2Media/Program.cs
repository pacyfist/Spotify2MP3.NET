using System;
using System.Linq;
using Terminal.Gui;
using Spotify2Media.UI;

namespace Spotify2Media;

class Program
{
    static void Main(string[] args)
    {
        string? defaultFolder = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--folder" && i + 1 < args.Length)
            {
                defaultFolder = args[i + 1];
                i++;
            }
        }

        Application.Init();
        Application.QuitKey = Key.C | Key.CtrlMask;

        // Set dark color scheme
        Colors.Base = new ColorScheme()
        {
            Normal = Terminal.Gui.Attribute.Make(Color.Gray, Color.Black),
            Focus = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
            HotNormal = Terminal.Gui.Attribute.Make(Color.BrightCyan, Color.Black),
            HotFocus = Terminal.Gui.Attribute.Make(Color.BrightCyan, Color.DarkGray),
            Disabled = Terminal.Gui.Attribute.Make(Color.DarkGray, Color.Black)
        };

        Colors.Dialog = new ColorScheme()
        {
            Normal = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
            Focus = Terminal.Gui.Attribute.Make(Color.Black, Color.Gray),
            HotNormal = Terminal.Gui.Attribute.Make(Color.BrightCyan, Color.DarkGray),
            HotFocus = Terminal.Gui.Attribute.Make(Color.BrightCyan, Color.Gray),
            Disabled = Terminal.Gui.Attribute.Make(Color.DarkGray, Color.DarkGray)
        };

        Colors.Error = new ColorScheme()
        {
            Normal = Terminal.Gui.Attribute.Make(Color.BrightRed, Color.Black),
            Focus = Terminal.Gui.Attribute.Make(Color.White, Color.BrightRed),
            HotNormal = Terminal.Gui.Attribute.Make(Color.BrightYellow, Color.Black),
            HotFocus = Terminal.Gui.Attribute.Make(Color.BrightYellow, Color.BrightRed),
            Disabled = Terminal.Gui.Attribute.Make(Color.DarkGray, Color.Black)
        };

        Application.Top.ColorScheme = Colors.Base;

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Application.MainLoop.Invoke(() =>
            {
                Application.Shutdown();
                Environment.Exit(0);
            });
        };

        Application.Top.Add(new MainWindow(defaultFolder));
        try
        {
            Application.Run();
        }
        finally
        {
            Application.Shutdown();
        }
    }
}
