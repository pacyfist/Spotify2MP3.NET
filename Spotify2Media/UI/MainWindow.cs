using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Spotify2Media.Models;
using Spotify2Media.Core;

namespace Spotify2Media.UI;

public class MainWindow : Window
{
    private TextField _csvPathField;
    private TextField _outputPathField;
    private CheckBox _deepSearchCheck;
    private ProgressBar _progressBar;
    private Label _statusLabel;
    private Button _convertBtn;
    private Config _config;
    private LogWindow _logWindow;
    private string? _defaultFolder;

    public MainWindow(string? defaultFolder = null) : base("Spotify2MP3 .NET")
    {
        ColorScheme = Colors.Base;
        _config = Config.Load();
        _defaultFolder = defaultFolder;
        
        var y = 1;
        Add(new Label("1) Select CSV File:") { X = 1, Y = y++ });
        _csvPathField = new TextField("") { X = 1, Y = y++, Width = Dim.Fill() - 15 };
        var browseCsvBtn = new Button("Browse") { X = Pos.Right(_csvPathField) + 1, Y = y - 1 };
        browseCsvBtn.Clicked += BrowseCsv;
        Add(_csvPathField, browseCsvBtn);

        y++;
        Add(new Label("2) Output Folder:") { X = 1, Y = y++ });
        _outputPathField = new TextField("") { X = 1, Y = y++, Width = Dim.Fill() - 15 };
        var browseFolderBtn = new Button("Browse") { X = Pos.Right(_outputPathField) + 1, Y = y - 1 };
        browseFolderBtn.Clicked += BrowseFolder;
        Add(_outputPathField, browseFolderBtn);

        y++;
        Add(new Label("3) Conversion Options:") { X = 1, Y = y++ });
        _deepSearchCheck = new CheckBox("Deep Search (Accurate but slower)") { X = 1, Y = y++, Checked = true };
        
        var settingsBtn = new Button("Settings") { X = 1, Y = y++ };
        settingsBtn.Clicked += OpenSettings;
        Add(_deepSearchCheck, settingsBtn);

        y++;
        _convertBtn = new Button("Convert Playlist") { X = Pos.Center(), Y = y++ };
        _convertBtn.Clicked += StartConversion;
        Add(_convertBtn);

        y++;
        Add(new Label("Status:") { X = 1, Y = y++ });
        _statusLabel = new Label("Waiting...") { X = 1, Y = y++, Width = Dim.Fill() };
        _progressBar = new ProgressBar() { X = 1, Y = y++, Width = Dim.Fill(), Fraction = 0f };
        Add(_statusLabel, _progressBar);

        _logWindow = new LogWindow {
            X = 0,
            Y = Pos.Bottom(_progressBar) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        Add(_logWindow);
    }

    private void BrowseCsv()
    {
        var dialog = new OpenDialog("Open CSV", "Select Spotify CSV File");
        dialog.AllowsMultipleSelection = false;
        dialog.AllowedFileTypes = new[] { ".csv" };
        if (!string.IsNullOrEmpty(_defaultFolder) && Directory.Exists(_defaultFolder))
        {
            dialog.DirectoryPath = _defaultFolder;
        }
        Application.Run(dialog);
        if (!dialog.Canceled)
        {
            var p = dialog.FilePath?.ToString() ?? (dialog.FilePaths.Count > 0 ? dialog.FilePaths[0] : "");
            if (!string.IsNullOrEmpty(p)) _csvPathField.Text = p;
        }
    }

    private void BrowseFolder()
    {
        var dialog = new OpenDialog("Output Folder", "Select destination directory");
        dialog.CanChooseFiles = false;
        dialog.CanChooseDirectories = true;
        if (!string.IsNullOrEmpty(_defaultFolder) && Directory.Exists(_defaultFolder))
        {
            dialog.DirectoryPath = _defaultFolder;
        }
        Application.Run(dialog);
        if (!dialog.Canceled)
        {
            var p = dialog.FilePath?.ToString() ?? (dialog.FilePaths.Count > 0 ? dialog.FilePaths[0] : "");
            if (!string.IsNullOrEmpty(p)) _outputPathField.Text = p;
        }
    }

    private void OpenSettings()
    {
        var settingsDialog = new SettingsDialog(_config);
        Application.Run(settingsDialog);
        _config.Save();
    }

    private async void StartConversion()
    {
        var csvPath = _csvPathField.Text.ToString();
        var outPath = _outputPathField.Text.ToString();

        if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
        {
            MessageBox.ErrorQuery("Error", "Invalid CSV path", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(outPath))
        {
            MessageBox.ErrorQuery("Error", "Invalid output directory", "OK");
            return;
        }

        _convertBtn.Enabled = false;

        try
        {
            using var reader = new StreamReader(csvPath);
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, HeaderValidated = null };
            using var csv = new CsvReader(reader, csvConfig);
            
            var tracks = csv.GetRecords<Track>().ToList();
            if (tracks.Count == 0)
            {
                MessageBox.ErrorQuery("Error", "No tracks found in CSV", "OK");
                _convertBtn.Enabled = true;
                return;
            }

            var playlistName = Path.GetFileNameWithoutExtension(csvPath);
            var playlistDir = Path.Combine(outPath, playlistName);

            var downloader = new Downloader(_config, playlistDir, _deepSearchCheck.Checked,
                status => Application.MainLoop.Invoke(() => _statusLabel.Text = status),
                progress => Application.MainLoop.Invoke(() => _progressBar.Fraction = Math.Min(progress / 100f, 1f)),
                (msg, isError) => _logWindow.Log(msg, isError)
            );

            var notFound = await downloader.DownloadPlaylistAsync(tracks);

            Application.MainLoop.Invoke(() => {
                _statusLabel.Text = $"✅ Completed. {tracks.Count - notFound.Count} downloaded, {notFound.Count} failed.";
                _progressBar.Fraction = 1.0f;
                _convertBtn.Enabled = true;
            });
        }
        catch (Exception ex)
        {
            Application.MainLoop.Invoke(() => {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
                _convertBtn.Enabled = true;
            });
        }
    }
}
