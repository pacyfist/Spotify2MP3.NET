using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Spotify2Media.Core;
using Spotify2Media.Models;
using Terminal.Gui;

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

    public MainWindow(string? defaultFolder = null)
        : base()
    {
        Title = "Spotify2MP3 .NET";
        ColorScheme = Colors.ColorSchemes["Base"];
        _config = Config.Load();
        _defaultFolder = defaultFolder;

        var y = 1;
        Add(
            new Label()
            {
                Text = "1) Select CSV File:",
                X = 1,
                Y = y++,
            }
        );
        _csvPathField = new TextField()
        {
            Text = "",
            X = 1,
            Y = y++,
            Width = (Dim.Fill() ?? 0) - 15,
        };
        var browseCsvBtn = new Button()
        {
            Text = "Browse",
            X = Pos.Right(_csvPathField) + 1,
            Y = y - 1,
        };
        browseCsvBtn.Accepting += (s, e) => BrowseCsv();
        Add(_csvPathField, browseCsvBtn);

        y++;
        Add(
            new Label()
            {
                Text = "2) Output Folder:",
                X = 1,
                Y = y++,
            }
        );
        _outputPathField = new TextField()
        {
            Text = "",
            X = 1,
            Y = y++,
            Width = (Dim.Fill() ?? 0) - 15,
        };
        var browseFolderBtn = new Button()
        {
            Text = "Browse",
            X = Pos.Right(_outputPathField) + 1,
            Y = y - 1,
        };
        browseFolderBtn.Accepting += (s, e) => BrowseFolder();
        Add(_outputPathField, browseFolderBtn);

        y++;
        Add(
            new Label()
            {
                Text = "3) Conversion Options:",
                X = 1,
                Y = y++,
            }
        );
        _deepSearchCheck = new CheckBox()
        {
            Text = "Deep Search (Accurate but slower)",
            X = 1,
            Y = y++,
            CheckedState = CheckState.Checked,
        };

        var settingsBtn = new Button()
        {
            Text = "Settings",
            X = 1,
            Y = y++,
        };
        settingsBtn.Accepting += (s, e) => OpenSettings();
        Add(_deepSearchCheck, settingsBtn);

        y++;
        _convertBtn = new Button()
        {
            Text = "Convert Playlist",
            X = Pos.Center(),
            Y = y++,
        };
        _convertBtn.Accepting += (s, e) => StartConversion();
        Add(_convertBtn);

        y++;
        Add(
            new Label()
            {
                Text = "Status:",
                X = 1,
                Y = y++,
            }
        );
        _statusLabel = new Label()
        {
            Text = "Waiting...",
            X = 1,
            Y = y++,
            Width = Dim.Fill(),
        };
        _progressBar = new ProgressBar()
        {
            X = 1,
            Y = y++,
            Width = Dim.Fill(),
            Fraction = 0f,
        };
        Add(_statusLabel, _progressBar);

        _logWindow = new LogWindow
        {
            X = 0,
            Y = Pos.Bottom(_progressBar) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        Add(_logWindow);
    }

    private void BrowseCsv()
    {
        var dialog = new OpenDialog { Title = "Open CSV" };
        dialog.AllowsMultipleSelection = false;
        dialog.AllowedTypes = new List<IAllowedType> { new AllowedType("CSV File", ".csv") };
        if (!string.IsNullOrEmpty(_defaultFolder) && Directory.Exists(_defaultFolder))
        {
            dialog.Path = _defaultFolder;
        }
        Application.Run(dialog);
        if (!dialog.Canceled)
        {
            var p = dialog.Path;
            if (!string.IsNullOrEmpty(p))
                _csvPathField.Text = p;
        }
    }

    private void BrowseFolder()
    {
        var dialog = new OpenDialog { Title = "Output Folder" };
        dialog.OpenMode = OpenMode.Directory;
        if (!string.IsNullOrEmpty(_defaultFolder) && Directory.Exists(_defaultFolder))
        {
            dialog.Path = _defaultFolder;
        }
        Application.Run(dialog);
        if (!dialog.Canceled)
        {
            var p = dialog.Path;
            if (!string.IsNullOrEmpty(p))
                _outputPathField.Text = p;
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
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HeaderValidated = null,
            };
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

            var downloader = new Downloader(
                _config,
                playlistDir,
                _deepSearchCheck.CheckedState == CheckState.Checked,
                status => Application.Invoke(() => _statusLabel.Text = status),
                progress =>
                    Application.Invoke(() => _progressBar.Fraction = Math.Min(progress / 100f, 1f)),
                (msg, isError) => _logWindow.Log(msg, isError)
            );

            var notFound = await downloader.DownloadPlaylistAsync(tracks);

            Application.Invoke(() =>
            {
                _statusLabel.Text =
                    $"✅ Completed. {tracks.Count - notFound.Count} downloaded, {notFound.Count} failed.";
                _progressBar.Fraction = 1.0f;
                _convertBtn.Enabled = true;
            });
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
                _convertBtn.Enabled = true;
            });
        }
    }
}
