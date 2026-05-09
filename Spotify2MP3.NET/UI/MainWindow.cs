using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
                Text = "1) CSV file or Spotify URL (playlist/album):",
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

    private void BrowseCsv() =>
        BrowseInto(
            _csvPathField,
            "Open CSV",
            OpenMode.File,
            new List<IAllowedType> { new AllowedType("CSV File", ".csv") }
        );

    private void BrowseFolder() =>
        BrowseInto(_outputPathField, "Output Folder", OpenMode.Directory);

    private void BrowseInto(
        TextField target,
        string title,
        OpenMode openMode,
        List<IAllowedType>? allowedTypes = null
    )
    {
        var dialog = new OpenDialog
        {
            Title = title,
            OpenMode = openMode,
            AllowsMultipleSelection = false,
        };
        if (allowedTypes != null)
            dialog.AllowedTypes = allowedTypes;
        if (!string.IsNullOrEmpty(_defaultFolder) && Directory.Exists(_defaultFolder))
            dialog.Path = _defaultFolder;

        Application.Run(dialog);
        if (!dialog.Canceled && !string.IsNullOrEmpty(dialog.Path))
            target.Text = dialog.Path;
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
        var input = _csvPathField.Text.ToString() ?? string.Empty;
        var outPath = _outputPathField.Text.ToString() ?? string.Empty;

        if (!ValidateConversionInputs(input, outPath, out var spotifyType, out var spotifyId, out var isSpotify))
            return;

        _convertBtn.Enabled = false;
        _conversionCts = new CancellationTokenSource();

        try
        {
            var (tracks, playlistName) = isSpotify
                ? await LoadTracksFromSpotifyAsync(spotifyType, spotifyId, _conversionCts.Token)
                : LoadTracksFromCsv(input);

            if (tracks.Count == 0)
            {
                ShowDialog("Error", "No tracks found in playlist", "Error", centerText: true);
                _convertBtn.Enabled = true;
                return;
            }

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

            Application.Invoke(() => ShowConversionComplete(tracks.Count, notFound));
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

    private bool ValidateConversionInputs(
        string input,
        string outPath,
        out SpotifyEntityType spotifyType,
        out string spotifyId,
        out bool isSpotify
    )
    {
        spotifyType = SpotifyEntityType.Playlist;
        spotifyId = string.Empty;
        isSpotify = false;

        if (string.IsNullOrWhiteSpace(input))
        {
            ShowDialog(
                "Error",
                "Provide a CSV file path or Spotify playlist URL",
                "Error",
                centerText: true
            );
            return false;
        }
        if (string.IsNullOrWhiteSpace(outPath))
        {
            ShowDialog("Error", "Invalid output directory", "Error", centerText: true);
            return false;
        }

        isSpotify = SpotifyUrl.TryParse(input, out spotifyType, out spotifyId);
        if (!isSpotify && !File.Exists(input))
        {
            ShowDialog(
                "Error",
                "Input is neither an existing CSV file nor a Spotify playlist/album URL",
                "Error",
                centerText: true
            );
            return false;
        }
        return true;
    }

    private async Task<(List<Track> Tracks, string PlaylistName)> LoadTracksFromSpotifyAsync(
        SpotifyEntityType type,
        string id,
        CancellationToken ct
    )
    {
        _statusLabel.Text = $"Fetching {type.ToString().ToLowerInvariant()} from Spotify...";
        using var fetcher = new SpotifyEmbedFetcher();
        var playlist = await fetcher.FetchAsync(type, id, ct);
        return (playlist.Tracks, SanitizeFolderName(playlist.Name));
    }

    private static (List<Track> Tracks, string PlaylistName) LoadTracksFromCsv(string path)
    {
        using var reader = new StreamReader(path);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null,
        };
        using var csv = new CsvReader(reader, csvConfig);
        var tracks = csv.GetRecords<Track>().ToList();
        return (tracks, Path.GetFileNameWithoutExtension(path));
    }

    private void ShowConversionComplete(int totalTracks, List<Track> notFound)
    {
        var downloaded = totalTracks - notFound.Count;
        var failed = notFound.Count;

        _statusLabel.Text = $"✅ Completed. {downloaded} downloaded, {failed} failed.";
        _progressBar.Fraction = 1.0f;
        _convertBtn.Enabled = true;

        ShowDialog("Conversion Complete", BuildCompletionSummary(totalTracks, notFound), "Dialog");
    }

    private static string BuildCompletionSummary(int totalTracks, List<Track> notFound)
    {
        var downloaded = totalTracks - notFound.Count;
        var failed = notFound.Count;
        var summary =
            $"Total tracks: {totalTracks}\nDownloaded:   {downloaded}\nFailed:       {failed}";
        if (notFound.Count > 0)
        {
            summary +=
                "\n\nFailed tracks:\n"
                + string.Join("\n", notFound.Select(t => $"  - {t.TrackName} — {t.ArtistNames}"));
        }
        return summary;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        var cleaned = sb.ToString().Trim();
        return string.IsNullOrEmpty(cleaned) ? "playlist" : cleaned;
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
