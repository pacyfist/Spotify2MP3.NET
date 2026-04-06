using System;
using System.Linq;
using Terminal.Gui;
using Spotify2MP3.NET.UI;

namespace Spotify2MP3.NET;

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
        Application.QuitKey = Key.C.WithCtrl;
        
        // Set dark color scheme
        var baseScheme = new ColorScheme()
        {
            Normal = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
            Focus = new Terminal.Gui.Attribute(Color.White, Color.DarkGray),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
            HotFocus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.DarkGray),
            Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
        };

        var dialogScheme = new ColorScheme()
        {
            Normal = new Terminal.Gui.Attribute(Color.White, Color.DarkGray),
            Focus = new Terminal.Gui.Attribute(Color.Black, Color.Gray),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.DarkGray),
            HotFocus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Gray),
            Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.DarkGray)
        };

        var errorScheme = new ColorScheme()
        {
            Normal = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
            Focus = new Terminal.Gui.Attribute(Color.White, Color.BrightRed),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
            HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.BrightRed),
            Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
        };

        Colors.ColorSchemes["Base"] = baseScheme;
        Colors.ColorSchemes["Dialog"] = dialogScheme;
        Colors.ColorSchemes["Error"] = errorScheme;

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Application.Invoke(() =>
            {
                Application.Shutdown();
                Environment.Exit(0);
            });
        };

        var mainWindow = new MainWindow(defaultFolder);
        mainWindow.ColorScheme = baseScheme;

        try
        {
            Application.Run(mainWindow);
        }
        finally
        {
            Application.Shutdown();
        }
    }
}
