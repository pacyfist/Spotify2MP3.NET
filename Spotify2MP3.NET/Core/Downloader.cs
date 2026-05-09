using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Spotify2MP3.NET.Models;

namespace Spotify2MP3.NET.Core;

public partial class Downloader(
    Config config,
    string outputDir,
    bool deepSearch,
    Action<string> statusCallback,
    Action<int> progressCallback,
    ConversionLogger logger
)
{
    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex AlphanumericOnlyRegex();

    [GeneratedRegex(@"[,/&]| feat\.| ft\.", RegexOptions.IgnoreCase)]
    private static partial Regex ArtistSeparatorRegex();

    private record TrackInfo(
        string Title,
        string SafeTitle,
        string SafeArtist,
        string ArtistPrimary,
        int? SpotifySec,
        List<string> Variants
    );

    private record VariantInfo(
        string BaseName,
        string Query,
        string SafeTitle,
        string SafeArtist,
        string ArtistPrimary,
        string Variant,
        int? SpotifySec
    );

    private enum SafeModeTier
    {
        Normal,
        LargeBatch,
        Aggressive,
    }

    private SafeModeTier GetSafeModeTier(int trackCount) =>
        trackCount >= 500 ? SafeModeTier.Aggressive
        : trackCount >= 250 ? SafeModeTier.LargeBatch
        : SafeModeTier.Normal;

    private List<string> GetSafeModeArgs(SafeModeTier tier) =>
        tier switch
        {
            SafeModeTier.Normal =>
            [
                "--sleep-interval", "2",
                "--max-sleep-interval", "5",
                "--sleep-requests", "1",
                "--limit-rate", "5M",
            ],
            SafeModeTier.LargeBatch =>
            [
                "--sleep-interval", "5",
                "--max-sleep-interval", "15",
                "--sleep-requests", "2",
                "--limit-rate", "2M",
                "--throttled-rate", "100K",
            ],
            SafeModeTier.Aggressive =>
            [
                "--sleep-interval", "10",
                "--max-sleep-interval", "30",
                "--sleep-requests", "5",
                "--limit-rate", "1M",
                "--throttled-rate", "100K",
            ],
            _ => [],
        };

    private int GetSafeModeDelay(SafeModeTier tier) =>
        tier switch
        {
            SafeModeTier.Normal => 3,
            SafeModeTier.LargeBatch => 8,
            SafeModeTier.Aggressive => 15,
            _ => 0,
        };

    public async Task<List<Track>> DownloadPlaylistAsync(
        List<Track> tracks,
        CancellationToken ct = default
    )
    {
        var notFound = new List<Track>();
        int total = tracks.Count;
        int i = 1;

        Directory.CreateDirectory(outputDir);

        using var coverArtFetcher = config.UseSpotifyCoverArt ? new SpotifyEmbedFetcher() : null;

        SafeModeTier? safeModeTier = null;
        if (config.SafeMode)
        {
            safeModeTier = GetSafeModeTier(total);
            logger.Log(
                $"Safe mode enabled — tier: {safeModeTier} ({total} tracks). "
                + "Downloads will be paced to avoid YouTube throttling.",
                false
            );
        }

        foreach (var track in tracks)
        {
            ct.ThrowIfCancellationRequested();
            track.TrackNumber = i;
            bool success = false;
            bool downloaded = false;

            // Step 1: Calculate names
            var trackInfo = CalculateNames(track);

            foreach (var variant in trackInfo.Variants)
            {
                ct.ThrowIfCancellationRequested();
                var variantInfo = GetVariantInfo(trackInfo, variant);

                // Step 2: Check if output file already exists
                logger.Log($"Processing: {variantInfo.BaseName}", false);
                var existingFile = FindExistingFile(variantInfo);

                if (existingFile != null)
                {
                    logger.Log($"[OK] Already exists: {Path.GetFileName(existingFile)}", false);
                    success = true;
                }
                else
                {
                    // Step 3: Search for the track
                    statusCallback($"[{i}/{total}] Processing: {variantInfo.Query}");
                    logger.Log($"Searching for: {variantInfo.Query}", false);

                    string downloadSpec = $"ytsearch1:{variantInfo.Query}";

                    if (deepSearch)
                    {
                        logger.Log("Performing deep search...", false);
                        downloadSpec = await PerformDeepSearchAsync(
                                variantInfo.Query,
                                variantInfo.SafeTitle,
                                variantInfo.SafeArtist,
                                variantInfo.Variant,
                                variantInfo.SpotifySec,
                                ct
                            )
                            .ConfigureAwait(false);
                        logger.Log($"Deep search result: {downloadSpec}", false);
                    }

                    // Step 4: Download the track (yt-dlp handles MP3 conversion if enabled)
                    var downloadedFile = await DownloadTrackAsync(variantInfo, downloadSpec, safeModeTier, ct)
                        .ConfigureAwait(false);

                    if (downloadedFile != null)
                    {
                        logger.Log(
                            $"[OK] Download successful: {Path.GetFileName(downloadedFile)}",
                            false
                        );

                        // Embed metadata (with optional Spotify cover art)
                        var coverImage = coverArtFetcher is null
                            ? null
                            : await ResolveCoverArtAsync(coverArtFetcher, track, ct).ConfigureAwait(false);

                        var metadata = new MetadataEmbedder();
                        logger.Log(
                            coverImage is { Length: > 0 }
                                ? "Embedding metadata (with Spotify cover art)..."
                                : "Embedding metadata...",
                            false
                        );
                        metadata.EmbedTags(
                            downloadedFile,
                            track.TrackName ?? "Unknown",
                            variantInfo.ArtistPrimary,
                            track.AlbumName ?? "Unknown",
                            (uint)track.TrackNumber,
                            coverImage
                        );
                        logger.Log(
                            $"Successfully finished processing: {variantInfo.BaseName}",
                            false
                        );
                        success = true;
                        downloaded = true;
                    }
                    else
                    {
                        logger.Log("[Error] Download failed", true);
                        success = false;
                        break;
                    }
                }
            }

            if (!success)
            {
                notFound.Add(track);
                logger.Log($"Failed to find or download: {track.TrackName}", true);
            }

            if (safeModeTier.HasValue && i < total && downloaded)
            {
                var delaySec = GetSafeModeDelay(safeModeTier.Value);
                logger.Log($"Safe mode: waiting {delaySec}s before next track...", false);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct).ConfigureAwait(false);
            }

            progressCallback((int)((double)i / total * 100));
            i++;
        }

        if (config.GenerateM3u)
        {
            GenerateM3uPlaylist();
        }

        return notFound;
    }

    private async Task<byte[]?> ResolveCoverArtAsync(
        SpotifyEmbedFetcher fetcher,
        Track track,
        CancellationToken ct
    )
    {
        // Album inputs already have AlbumArtUrl populated. Playlist inputs have only the
        // track ID — resolve the per-track album cover URL via /embed/track/{id}.
        var url = track.AlbumArtUrl;
        if (string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(track.SpotifyTrackId))
        {
            url = await fetcher
                .FetchTrackCoverArtUrlAsync(track.SpotifyTrackId, ct)
                .ConfigureAwait(false);
        }
        if (string.IsNullOrEmpty(url))
            return null;

        var bytes = await fetcher.FetchImageBytesAsync(url, ct).ConfigureAwait(false);
        return bytes.Length > 0 ? bytes : null;
    }

    private TrackInfo CalculateNames(Track track)
    {
        var safeTitle = AlphanumericOnlyRegex().Replace(track.TrackName ?? "", "").Trim();
        var artistRaw = track.ArtistNames ?? "";
        var artistPrimary = ArtistSeparatorRegex().Split(artistRaw)[0].Trim();
        var safeArtist = AlphanumericOnlyRegex().Replace(artistPrimary, "").Trim();

        int.TryParse(track.DurationMs, out int spotifyMs);
        int? spotifySec = spotifyMs > 0 ? spotifyMs / 1000 : null;

        var variants = config.Variants.ToList();
        if (variants.Count == 0)
            variants.Add("");
        if ((track.TrackName ?? "").ToLower().Contains("instrumental"))
        {
            variants.Insert(0, "instrumental");
        }

        return new TrackInfo(track.TrackName ?? "", safeTitle, safeArtist, artistPrimary, spotifySec, variants);
    }

    private VariantInfo GetVariantInfo(TrackInfo trackInfo, string variant)
    {
        var variantSuffix = string.IsNullOrEmpty(variant) ? "" : $" - {variant}";
        var baseName = $"{trackInfo.SafeArtist} - {trackInfo.SafeTitle}{variantSuffix}";

        var parts = new List<string> { trackInfo.Title };
        if (
            !string.IsNullOrWhiteSpace(trackInfo.ArtistPrimary)
            && trackInfo.ArtistPrimary.ToLower() != "unknown"
        )
            parts.Add(trackInfo.ArtistPrimary);
        if (!string.IsNullOrWhiteSpace(variant))
            parts.Add(variant);

        var query = string.Join(" ", parts);

        return new VariantInfo(
            baseName,
            query,
            trackInfo.SafeTitle,
            trackInfo.SafeArtist,
            trackInfo.ArtistPrimary,
            variant,
            trackInfo.SpotifySec
        );
    }

    private string? FindExistingFile(VariantInfo variantInfo)
    {
        var mp3Path = Path.Combine(outputDir, variantInfo.BaseName + ".mp3");
        return File.Exists(mp3Path) ? mp3Path : null;
    }

    private List<string> BuildDownloadArgs(
        string outputTemplatePath,
        string downloadSpec,
        SafeModeTier? safeModeTier
    )
    {
        var args = new List<string>
        {
            "--ignore-config",
            "--format", "bestaudio",
            "--output", outputTemplatePath,
            "--no-playlist",
            "--embed-thumbnail",
            "--embed-metadata",
            "--extract-audio",
            "--audio-format", "mp3",
            "--audio-quality", "0",
        };

        if (config.ExcludeInstrumentals)
            args.AddRange(["--match-filter", "title!*=instrumental"]);

        if (safeModeTier.HasValue)
            args.AddRange(GetSafeModeArgs(safeModeTier.Value));

        args.Add(downloadSpec);
        return args;
    }

    private async Task<string?> DownloadTrackAsync(
        VariantInfo variantInfo,
        string downloadSpec,
        SafeModeTier? safeModeTier,
        CancellationToken ct
    )
    {
        var outputTemplate = Path.Combine(outputDir, variantInfo.BaseName + ".%(ext)s");
        var args = BuildDownloadArgs(outputTemplate, downloadSpec, safeModeTier);

        logger.Log(
            $"yt-dlp {string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}",
            false
        );

        var (exitCode, stdout, stderr) = await RunCommandAsync("yt-dlp", args, ct)
            .ConfigureAwait(false);

        if (exitCode == 0 || stdout.Contains("has already been recorded in the archive"))
        {
            var downloadedFile = FindExistingFile(variantInfo);
            if (downloadedFile != null)
                return downloadedFile;
            logger.Log($"File not found after download: {variantInfo.BaseName}", true);
        }
        else
        {
            logger.Log($"yt-dlp download failed: {stderr}", true);
        }

        return null;
    }

    private void GenerateM3uPlaylist()
    {
        var m3uPath = Path.Combine(outputDir, "playlist.m3u");
        var audioFiles = Directory
            .GetFiles(outputDir)
            .Where(f => f.EndsWith(".mp3"))
            .OrderBy(File.GetCreationTime)
            .ToList();
        using var sw = new StreamWriter(m3uPath);
        sw.WriteLine("#EXTM3U");
        foreach (var file in audioFiles)
        {
            sw.WriteLine($"#EXTINF:-1,{Path.GetFileNameWithoutExtension(file)}");
            sw.WriteLine(Path.GetFileName(file));
        }
    }

    private record VideoInfo(string Title, string Uploader, double Duration);

    private static VideoInfo ReadVideoInfo(JsonElement element)
    {
        var title = element.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var uploader = element.TryGetProperty("uploader", out var u)
            ? (u.GetString() ?? "").ToLower()
            : "";
        var duration = element.TryGetProperty("duration", out var d) ? d.GetDouble() : 0;
        return new VideoInfo(title, uploader, duration);
    }

    private bool IsDurationInRange(double duration) =>
        duration >= config.DurationMin && duration <= config.DurationMax;

    private static bool MatchesArtist(string uploader, string safeArtist) =>
        string.IsNullOrEmpty(safeArtist) || uploader.Contains(safeArtist.ToLower());

    private async Task<string> PerformDeepSearchAsync(
        string query,
        string safeTitle,
        string safeArtist,
        string variant,
        int? spotifySec,
        CancellationToken ct
    )
    {
        var firstMatch = await TryFirstResultMatchAsync(query, safeTitle, safeArtist, spotifySec, ct)
            .ConfigureAwait(false);
        if (firstMatch != null)
            return firstMatch;

        var bestOfTop = await TryBestOfTopResultsAsync(query, safeTitle, safeArtist, variant, spotifySec, ct)
            .ConfigureAwait(false);
        if (bestOfTop != null)
            return bestOfTop;

        return $"ytsearch1:{query}";
    }

    private async Task<string?> TryFirstResultMatchAsync(
        string query,
        string safeTitle,
        string safeArtist,
        int? spotifySec,
        CancellationToken ct
    )
    {
        var (code, stdout, _) = await RunCommandAsync(
                "yt-dlp",
                new[] { "--dump-single-json", "--no-playlist", $"ytsearch1:{query}" },
                ct
            )
            .ConfigureAwait(false);
        if (code != 0)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            if (
                !doc.RootElement.TryGetProperty("entries", out var entries)
                || entries.GetArrayLength() == 0
            )
                return null;

            var top = entries[0];
            var info = ReadVideoInfo(top);
            var titleLow = info.Title.ToLower();
            var safeTitleLow = safeTitle.ToLower();

            bool titleMatches = safeTitleLow.Contains(titleLow) || titleLow.Contains(safeTitleLow);
            bool spotifyDurationMatches =
                !spotifySec.HasValue || Math.Abs(info.Duration - spotifySec.Value) <= 10;

            if (
                titleMatches
                && MatchesArtist(info.Uploader, safeArtist)
                && spotifyDurationMatches
                && IsDurationInRange(info.Duration)
                && top.TryGetProperty("webpage_url", out var url)
            )
            {
                return url.GetString() ?? $"ytsearch1:{query}";
            }
        }
        catch (JsonException ex)
        {
            logger.Log($"JSON parsing error in deep search (step 1): {ex.Message}", true);
        }
        catch (Exception ex)
        {
            logger.Log($"Unexpected error in deep search (step 1): {ex.Message}", true);
        }

        return null;
    }

    private async Task<string?> TryBestOfTopResultsAsync(
        string query,
        string safeTitle,
        string safeArtist,
        string variant,
        int? spotifySec,
        CancellationToken ct
    )
    {
        var (code, stdout, _) = await RunCommandAsync(
                "yt-dlp",
                new[]
                {
                    "--flat-playlist",
                    "--dump-single-json",
                    "--no-playlist",
                    $"ytsearch3:{query}",
                },
                ct
            )
            .ConfigureAwait(false);
        if (code != 0)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            if (!doc.RootElement.TryGetProperty("entries", out var entries))
                return null;

            var scored = new List<(int score, string url)>();
            foreach (var entry in entries.EnumerateArray().Take(3))
            {
                var candidate = await ScoreCandidateAsync(
                        entry,
                        safeTitle,
                        safeArtist,
                        variant,
                        spotifySec,
                        ct
                    )
                    .ConfigureAwait(false);
                if (candidate.HasValue)
                    scored.Add(candidate.Value);
            }

            if (scored.Count > 0)
                return scored.OrderByDescending(x => x.score).First().url;
        }
        catch (JsonException ex)
        {
            logger.Log($"JSON parsing error in deep search (step 3): {ex.Message}", true);
        }
        catch (Exception ex)
        {
            logger.Log($"Unexpected error in deep search (step 3): {ex.Message}", true);
        }

        return null;
    }

    private async Task<(int score, string url)?> ScoreCandidateAsync(
        JsonElement entry,
        string safeTitle,
        string safeArtist,
        string variant,
        int? spotifySec,
        CancellationToken ct
    )
    {
        if (!entry.TryGetProperty("id", out var idProp))
            return null;

        var url = $"https://www.youtube.com/watch?v={idProp.GetString()}";
        var (code, stdout, _) = await RunCommandAsync(
                "yt-dlp",
                new[] { "--dump-single-json", "--no-playlist", url },
                ct
            )
            .ConfigureAwait(false);
        if (code != 0)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var info = ReadVideoInfo(doc.RootElement);
            var titleLow = info.Title.ToLower();

            if (!IsDurationInRange(info.Duration))
                return null;
            if (titleLow.Contains("#shorts"))
                return null;
            if (!MatchesArtist(info.Uploader, safeArtist))
                return null;
            if (!string.IsNullOrEmpty(variant) && !titleLow.Contains(variant.ToLower()))
                return null;

            int score = titleLow.StartsWith(safeTitle.ToLower()) ? 100 : 80;
            if (spotifySec.HasValue)
                score -= (int)Math.Abs(info.Duration - spotifySec.Value);
            return (score, url);
        }
        catch (JsonException ex)
        {
            logger.Log($"JSON parsing error for video info: {ex.Message}", true);
            return null;
        }
    }

    private async Task<(int exitCode, string stdout, string stderr)> RunCommandAsync(
        string command,
        IEnumerable<string> args,
        CancellationToken ct
    )
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            return (
                process.ExitCode,
                await stdoutTask.ConfigureAwait(false),
                await stderrTask.ConfigureAwait(false)
            );
        }
        catch (OperationCanceledException)
        {
            return (-1, "", "Operation cancelled by user.");
        }
        catch (Exception ex)
        {
            return (-1, "", $"Failed to start {command}: {ex.Message}");
        }
    }
}
