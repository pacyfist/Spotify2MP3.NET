using Spotify2MP3.NET.Core;
using Spotify2MP3.NET.Models;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Spotify2MP3.NET.UI;

public class MainWindow : Window
{
    private readonly TextField _csvPathField;
    private readonly TextField _outputPathField;
    private readonly CheckBox _deepSearchCheck;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly Button _convertBtn;
    private readonly Config _config;
    private readonly LogWindow _logWindow;
    private readonly string? _defaultFolder;

    private CancellationTokenSource? _conversionCts;

    public MainWindow(string? defaultFolder = null, string? defaultSource = null)
    {
        Title = "Spotify2MP3.NET";
        SchemeName = "Base";
        _config = Config.Load();
        _defaultFolder = defaultFolder;

        var y = 1;
        Add(
            new Label
            {
                Text = "1) CSV file or Spotify URL (playlist/album):",
                X = 1,
                Y = y++,
            }
        );
        _csvPathField = new TextField
        {
            Text = defaultSource ?? "",
            X = 1,
            Y = y++,
            Width = Dim.Fill() - 15,
        };
        var browseCsvBtn = new Button
        {
            Text = "_Browse",
            X = Pos.Right(_csvPathField) + 1,
            Y = y - 1,
        };
        browseCsvBtn.Accepting += (s, e) =>
        {
            e.Handled = true;
            BrowseCsv();
        };
        Add(_csvPathField, browseCsvBtn);

        y++;
        Add(
            new Label
            {
                Text = "2) Output Folder:",
                X = 1,
                Y = y++,
            }
        );
        _outputPathField = new TextField
        {
            Text = defaultFolder ?? "",
            X = 1,
            Y = y++,
            Width = Dim.Fill() - 15,
        };
        var browseFolderBtn = new Button
        {
            Text = "Br_owse",
            X = Pos.Right(_outputPathField) + 1,
            Y = y - 1,
        };
        browseFolderBtn.Accepting += (s, e) =>
        {
            e.Handled = true;
            BrowseFolder();
        };
        Add(_outputPathField, browseFolderBtn);

        y++;
        Add(
            new Label
            {
                Text = "3) Conversion Options:",
                X = 1,
                Y = y++,
            }
        );
        _deepSearchCheck = new CheckBox
        {
            Text = "_Deep Search (Accurate but slower)",
            X = 1,
            Y = y++,
            Value = CheckState.Checked,
        };

        var settingsBtn = new Button
        {
            Text = "_Settings",
            X = 1,
            Y = y++,
        };
        settingsBtn.Accepting += (s, e) =>
        {
            e.Handled = true;
            OpenSettings();
        };
        Add(_deepSearchCheck, settingsBtn);

        y++;
        _convertBtn = new Button
        {
            Text = "_Convert Playlist",
            IsDefault = true,
            X = Pos.Center(),
            Y = y++,
        };
        _convertBtn.Accepting += (s, e) =>
        {
            e.Handled = true;
            StartConversion();
        };
        Add(_convertBtn);

        y++;
        Add(
            new Label
            {
                Text = "Status:",
                X = 1,
                Y = y++,
            }
        );
        _statusLabel = new Label
        {
            Text = "Waiting...",
            X = 1,
            Y = y++,
            Width = Dim.Fill(),
        };
        _progressBar = new ProgressBar
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
        using var dialog = new OpenDialog
        {
            Title = title,
            OpenMode = openMode,
            AllowsMultipleSelection = false,
        };
        if (allowedTypes != null)
            dialog.AllowedTypes = allowedTypes;
        if (!string.IsNullOrEmpty(_defaultFolder) && Directory.Exists(_defaultFolder))
            dialog.Path = _defaultFolder;

        App!.Run(dialog);
        if (!dialog.Canceled && !string.IsNullOrEmpty(dialog.Path))
            target.Text = dialog.Path;
    }

    private void OpenSettings()
    {
        using var settingsDialog = new SettingsDialog(_config);
        App!.Run(settingsDialog);
        _config.Save();
    }

    private async void StartConversion()
    {
        var input = _csvPathField.Text.ToString() ?? string.Empty;
        var outPath = _outputPathField.Text.ToString() ?? string.Empty;

        if (
            !ValidateConversionInputs(
                input,
                outPath,
                out var spotifyType,
                out var spotifyId,
                out var isSpotify
            )
        )
            return;

        _convertBtn.Enabled = false;
        _conversionCts = new CancellationTokenSource();

        IApplication app = App!;

        try
        {
            var (tracks, playlistName) = isSpotify
                ? await LoadTracksFromSpotifyAsync(spotifyType, spotifyId, _conversionCts.Token)
                : PlaylistLoader.LoadFromCsv(input);

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
                _deepSearchCheck.Value == CheckState.Checked,
                status => app.Invoke(() => _statusLabel.Text = status),
                progress =>
                    app.Invoke(() => _progressBar.Fraction = Math.Min(progress / 100f, 1f)),
                logger
            );

            var notFound = await downloader.DownloadPlaylistAsync(tracks, _conversionCts.Token);

            app.Invoke(() => ShowConversionComplete(tracks.Count, notFound));
        }
        catch (OperationCanceledException)
        {
            app.Invoke(() =>
            {
                _statusLabel.Text = "❌ Conversion cancelled.";
                _convertBtn.Enabled = true;
            });
        }
        catch (Exception ex)
        {
            app.Invoke(() =>
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
        return await PlaylistLoader.LoadFromSpotifyAsync(type, id, ct);
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

    private void ShowDialog(string title, string message, string schemeName, bool centerText = false)
    {
        using var dialog = new Dialog
        {
            Title = title,
            SchemeName = schemeName,
            Width = Dim.Auto(minimumContentDim: 40, maximumContentDim: Dim.Percent(80)),
            Height = Dim.Auto(minimumContentDim: 6, maximumContentDim: Dim.Percent(80)),
        };

        var label = new Label
        {
            Text = message,
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
        };
        if (centerText)
            label.TextAlignment = Alignment.Center;

        var okBtn = new Button
        {
            Text = "_OK",
            IsDefault = true,
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
        };
        okBtn.Accepting += (s, e) =>
        {
            e.Handled = true;
            App!.RequestStop();
        };

        dialog.Add(label, okBtn);
        App!.Run(dialog);
    }
}
