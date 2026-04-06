using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terminal.Gui;

namespace Spotify2MP3.NET.UI;

public class LogWindow : Window
{
    private ListView _listView;
    private ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();

    public LogWindow()
        : base()
    {
        Title = "Conversion Log";
        ColorScheme = Colors.ColorSchemes["Base"];
        _listView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
        };
        _listView.SetSource<LogEntry>(_logEntries);

        Add(_listView);
    }

    public void Log(string message, bool isError = false)
    {
        Application.Invoke(() =>
        {
            // Using [Error] prefix as a fallback for color
            string prefix = isError ? "[ERROR] " : "[INFO] ";
            _logEntries.Add(new LogEntry { Message = prefix + message, IsError = isError });

            if (_logEntries.Count > 0)
                _listView.SelectedItem = _logEntries.Count - 1;

            SetNeedsDraw();
        });
    }

    private class LogEntry
    {
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }

        public override string ToString() => Message;
    }
}
