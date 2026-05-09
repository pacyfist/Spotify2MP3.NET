using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Spotify2MP3.NET.UI;

public class LogWindow : Window
{
    private readonly ListView _listView;
    private readonly ObservableCollection<LogEntry> _logEntries = [];

    public LogWindow()
    {
        Title = "Conversion Log";
        SchemeName = "Base";
        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _listView.SetSource<LogEntry>(_logEntries);

        Add(_listView);
    }

    public void Log(string message, bool isError = false)
    {
        App?.Invoke(() =>
        {
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
