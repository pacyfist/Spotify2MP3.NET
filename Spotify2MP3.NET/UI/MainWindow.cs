using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Spotify2MP3.NET.Core;
using Spotify2MP3.NET.Models;
using Terminal.Gui;

namespace Spotify2MP3.NET.UI;

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
        Title = "Spotify2MP3.NET";
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

    private CancellationTokenSource? _conversionCts;

    private async void StartConversion()
    {
        var csvPath = _csvPathField.Text.ToString();
        var outPath = _outputPathField.Text.ToString();

        if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
        {
            ShowDialog("Error", "Invalid CSV path", "Error", centerText: true);
            return;
        }
        if (string.IsNullOrWhiteSpace(outPath))
        {
            ShowDialog("Error", "Invalid output directory", "Error", centerText: true);
            return;
        }

        _convertBtn.Enabled = false;
        _conversionCts = new CancellationTokenSource();

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
                ShowDialog("Error", "No tracks found in CSV", "Error", centerText: true);
                _convertBtn.Enabled = true;
                return;
            }

            var playlistName = Path.GetFileNameWithoutExtension(csvPath);
            var playlistDir = Path.Combine(outPath, playlistName);

            Directory.CreateDirectory(playlistDir);
            using var logger = new ConversionLogger(
                playlistDir,
                (msg, isError) => _logWindow.Log(msg, isError)
            );

            var downloader = new Downloader(
                _config,
                playlistDir,
                _deepSearchCheck.CheckedState == CheckState.Checked,
                status => Application.Invoke(() => _statusLabel.Text = status),
                progress =>
                    Application.Invoke(() => _progressBar.Fraction = Math.Min(progress / 100f, 1f)),
                logger
            );

            var notFound = await downloader.DownloadPlaylistAsync(tracks, _conversionCts.Token);

            var downloaded = tracks.Count - notFound.Count;
            var failed = notFound.Count;

            Application.Invoke(() =>
            {
                _statusLabel.Text = $"✅ Completed. {downloaded} downloaded, {failed} failed.";
                _progressBar.Fraction = 1.0f;
                _convertBtn.Enabled = true;

                var summary =
                    $"Total tracks: {tracks.Count}\nDownloaded:   {downloaded}\nFailed:       {failed}";
                if (notFound.Count > 0)
                {
                    summary +=
                        "\n\nFailed tracks:\n"
                        + string.Join(
                            "\n",
                            notFound.Select(t => $"  - {t.TrackName} — {t.ArtistNames}")
                        );
                }

                ShowDialog("Conversion Complete", summary, "Dialog");
            });
        }
        catch (OperationCanceledException)
        {
            Application.Invoke(() =>
            {
                _statusLabel.Text = "❌ Conversion cancelled.";
                _convertBtn.Enabled = true;
            });
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                ShowDialog("Error", ex.Message, "Error", centerText: true);
                _convertBtn.Enabled = true;
            });
        }
        finally
        {
            _conversionCts?.Dispose();
            _conversionCts = null;
        }
    }

    private static void ShowDialog(
        string title,
        string message,
        string colorScheme,
        bool centerText = false
    )
    {
        var dialog = new Dialog
        {
            Title = title,
            Width = 60,
            Height = 14,
            ColorScheme = Colors.ColorSchemes[colorScheme],
        };

        var label = new Label
        {
            Text = message,
            Width = Dim.Fill(2),
        };

        if (centerText)
        {
            label.TextAlignment = Alignment.Center;
            label.X = 1;
            label.Y = Pos.Center();
        }
        else
        {
            label.X = 1;
            label.Y = 1;
            label.Height = Dim.Fill(2);
        }

        var okBtn = new Button
        {
            Text = "OK",
            IsDefault = true,
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
        };
        okBtn.Accepting += (s, e) => Application.RequestStop();

        dialog.Add(label, okBtn);
        Application.Run(dialog);
    }
}
