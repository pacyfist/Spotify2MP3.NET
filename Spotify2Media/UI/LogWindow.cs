using System;
using System.Collections.Generic;
using Terminal.Gui;

namespace Spotify2Media.UI;

public class LogWindow : Window
{
    private ListView _listView;
    private List<LogEntry> _logEntries = new List<LogEntry>();

    public LogWindow() : base("Conversion Log")
    {
        ColorScheme = Colors.Base;
        _listView = new ListView(_logEntries)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false
        };

        Add(_listView);
    }

    public void Log(string message, bool isError = false)
    {
        Application.MainLoop.Invoke(() => {
            // Using [Error] prefix as a fallback for color
            string prefix = isError ? "[ERROR] " : "[INFO] ";
            _logEntries.Add(new LogEntry { Message = prefix + message, IsError = isError });
            _listView.SetSource(_logEntries);
            
            if (_logEntries.Count > 0)
                _listView.SelectedItem = _logEntries.Count - 1;
                
            SetNeedsDisplay();
        });
    }

    private class LogEntry
    {
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public override string ToString() => Message;
    }
}
