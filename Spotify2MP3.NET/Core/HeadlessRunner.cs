using Spotify2MP3.NET.Models;

namespace Spotify2MP3.NET.Core;

public static class HeadlessRunner
{
    public const int ExitSuccess = 0;
    public const int ExitPartialFailure = 1;
    public const int ExitFatal = 2;
    public const int ExitCancelled = 130;

    public static async Task<int> RunAsync(
        string source,
        string folder,
        Config config,
        bool deepSearch,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            Console.Error.WriteLine("error: --source is empty");
            return ExitFatal;
        }
        if (string.IsNullOrWhiteSpace(folder))
        {
            Console.Error.WriteLine("error: --folder is empty");
            return ExitFatal;
        }

        var isSpotify = SpotifyUrl.TryParse(source, out var spotifyType, out var spotifyId);
        if (!isSpotify && !File.Exists(source))
        {
            Console.Error.WriteLine(
                $"error: --source '{source}' is neither an existing CSV file nor a Spotify playlist/album URL"
            );
            return ExitFatal;
        }

        List<Track> tracks;
        string playlistName;
        try
        {
            if (isSpotify)
            {
                Console.WriteLine(
                    $"Fetching {spotifyType.ToString().ToLowerInvariant()} from Spotify..."
                );
                (tracks, playlistName) = await PlaylistLoader.LoadFromSpotifyAsync(
                    spotifyType,
                    spotifyId,
                    ct
                );
            }
            else
            {
                Console.WriteLine($"Reading CSV: {source}");
                (tracks, playlistName) = PlaylistLoader.LoadFromCsv(source);
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled");
            return ExitCancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to load source: {ex.Message}");
            return ExitFatal;
        }

        if (tracks.Count == 0)
        {
            Console.Error.WriteLine("error: no tracks found in source");
            return ExitFatal;
        }

        string playlistDir;
        try
        {
            playlistDir = Path.Combine(folder, playlistName);
            Directory.CreateDirectory(playlistDir);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to create output directory: {ex.Message}");
            return ExitFatal;
        }

        Console.WriteLine($"Output: {playlistDir}");
        Console.WriteLine($"Tracks: {tracks.Count}");
        Console.WriteLine($"Deep search: {(deepSearch ? "on" : "off")}");
        Console.WriteLine();

        int lastReportedPct = -1;
        try
        {
            using var logger = new ConversionLogger(
                playlistDir,
                (msg, isError) =>
                {
                    var writer = isError ? Console.Error : Console.Out;
                    writer.WriteLine(msg);
                }
            );

            var downloader = new Downloader(
                config,
                playlistDir,
                deepSearch,
                status => Console.WriteLine($"[status] {status}"),
                progress =>
                {
                    if (progress != lastReportedPct && progress % 5 == 0)
                    {
                        lastReportedPct = progress;
                        Console.WriteLine($"[progress] {progress}%");
                    }
                },
                logger
            );

            var notFound = await downloader.DownloadPlaylistAsync(tracks, ct);

            var downloaded = tracks.Count - notFound.Count;
            Console.WriteLine();
            Console.WriteLine($"Total tracks: {tracks.Count}");
            Console.WriteLine($"Downloaded:   {downloaded}");
            Console.WriteLine($"Failed:       {notFound.Count}");
            if (notFound.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Failed tracks:");
                foreach (var t in notFound)
                    Console.WriteLine($"  - {t.TrackName} — {t.ArtistNames}");
                return ExitPartialFailure;
            }
            return ExitSuccess;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled");
            return ExitCancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitFatal;
        }
    }
}
