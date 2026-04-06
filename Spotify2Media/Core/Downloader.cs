using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Spotify2Media.Models;

namespace Spotify2Media.Core;

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

    public async Task<List<Track>> DownloadPlaylistAsync(
        List<Track> tracks,
        CancellationToken ct = default
    )
    {
        var notFound = new List<Track>();
        int total = tracks.Count;
        int i = 1;

        Directory.CreateDirectory(outputDir);

        foreach (var track in tracks)
        {
            ct.ThrowIfCancellationRequested();
            track.TrackNumber = i;
            bool success = false;

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
                    var downloadedFile = await DownloadTrackAsync(variantInfo, downloadSpec, ct)
                        .ConfigureAwait(false);

                    if (downloadedFile != null)
                    {
                        logger.Log(
                            $"[OK] Download successful: {Path.GetFileName(downloadedFile)}",
                            false
                        );

                        // Embed metadata
                        var metadata = new MetadataEmbedder();
                        logger.Log("Embedding metadata...", false);
                        metadata.EmbedTags(
                            downloadedFile,
                            track.TrackName ?? "Unknown",
                            variantInfo.ArtistPrimary,
                            track.AlbumName ?? "Unknown",
                            (uint)track.TrackNumber
                        );
                        logger.Log(
                            $"Successfully finished processing: {variantInfo.BaseName}",
                            false
                        );
                        success = true;
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

            progressCallback((int)((double)i / total * 100));
            i++;
        }

        if (config.GenerateM3u)
        {
            GenerateM3uPlaylist();
        }

        return notFound;
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

        return new TrackInfo(safeTitle, safeArtist, artistPrimary, spotifySec, variants);
    }

    private VariantInfo GetVariantInfo(TrackInfo trackInfo, string variant)
    {
        var variantSuffix = string.IsNullOrEmpty(variant) ? "" : $" - {variant}";
        var baseName = $"{trackInfo.SafeArtist} - {trackInfo.SafeTitle}{variantSuffix}";

        var parts = new List<string> { trackInfo.SafeTitle };
        if (
            !string.IsNullOrWhiteSpace(trackInfo.SafeArtist)
            && trackInfo.SafeArtist.ToLower() != "unknown"
        )
            parts.Add(trackInfo.SafeArtist);
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

    private async Task<string?> DownloadTrackAsync(
        VariantInfo variantInfo,
        string downloadSpec,
        CancellationToken ct
    )
    {
        var tmpl = variantInfo.BaseName + ".%(ext)s";
        var args = new List<string>
        {
            "--ignore-config",
            "--format",
            "bestaudio",
            "--output",
            Path.Combine(outputDir, tmpl),
            "--no-playlist",
            "--embed-thumbnail",
            "--embed-metadata",
            "--extract-audio",
            "--audio-format",
            "mp3",
            "--audio-quality",
            "0",
        };

        if (config.ExcludeInstrumentals)
        {
            args.AddRange(["--match-filter", "title!*=instrumental"]);
        }

        args.Add(downloadSpec);

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
            {
                return downloadedFile;
            }
            else
            {
                logger.Log($"File not found after download: {variantInfo.BaseName}", true);
            }
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

    private async Task<string> PerformDeepSearchAsync(
        string query,
        string safeTitle,
        string safeArtist,
        string variant,
        int? spotifySec,
        CancellationToken ct
    )
    {
        var (code1, stdout1, _) = await RunCommandAsync(
                "yt-dlp",
                new[] { "--dump-single-json", "--no-playlist", $"ytsearch1:{query}" },
                ct
            )
            .ConfigureAwait(false);
        if (code1 == 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(stdout1);
                var root = doc.RootElement;
                if (root.TryGetProperty("entries", out var entries) && entries.GetArrayLength() > 0)
                {
                    var top = entries[0];
                    var vidTitle = top.TryGetProperty("title", out var t)
                        ? t.GetString() ?? ""
                        : "";
                    var uploader = top.TryGetProperty("uploader", out var u)
                        ? (u.GetString() ?? "").ToLower()
                        : "";
                    var duration = top.TryGetProperty("duration", out var d) ? d.GetDouble() : 0;

                    bool passes =
                        safeTitle.ToLower().Contains(vidTitle.ToLower())
                        || vidTitle.ToLower().Contains(safeTitle.ToLower());
                    if (!string.IsNullOrEmpty(safeArtist))
                        passes &= uploader.Contains(safeArtist.ToLower());
                    if (spotifySec.HasValue)
                        passes &= Math.Abs(duration - spotifySec.Value) <= 10;
                    passes &= duration >= config.DurationMin && duration <= config.DurationMax;

                    if (passes && top.TryGetProperty("webpage_url", out var url))
                    {
                        return url.GetString() ?? $"ytsearch1:{query}";
                    }
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
        }

        var (code3, stdout3, _) = await RunCommandAsync(
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
        if (code3 == 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(stdout3);
                var root = doc.RootElement;
                if (root.TryGetProperty("entries", out var entries))
                {
                    var scored = new List<(int score, string url)>();
                    foreach (var entry in entries.EnumerateArray().Take(3))
                    {
                        if (entry.TryGetProperty("id", out var idProp))
                        {
                            var vid = idProp.GetString();
                            var url = $"https://www.youtube.com/watch?v={vid}";
                            var (codeI, stdoutI, _) = await RunCommandAsync(
                                    "yt-dlp",
                                    new[] { "--dump-single-json", "--no-playlist", url },
                                    ct
                                )
                                .ConfigureAwait(false);
                            if (codeI == 0)
                            {
                                try
                                {
                                    using var iDoc = JsonDocument.Parse(stdoutI);
                                    var info = iDoc.RootElement;
                                    var rawTitle = info.TryGetProperty("title", out var t)
                                        ? t.GetString() ?? ""
                                        : "";
                                    var lowTitle = rawTitle.ToLower();
                                    var dur2 = info.TryGetProperty("duration", out var d)
                                        ? d.GetDouble()
                                        : 0;
                                    var up2 = info.TryGetProperty("uploader", out var u)
                                        ? (u.GetString() ?? "").ToLower()
                                        : "";

                                    if (dur2 < config.DurationMin || dur2 > config.DurationMax)
                                        continue;
                                    if (lowTitle.Contains("#shorts"))
                                        continue;
                                    if (
                                        !string.IsNullOrEmpty(safeArtist)
                                        && !up2.Contains(safeArtist.ToLower())
                                    )
                                        continue;
                                    if (
                                        !string.IsNullOrEmpty(variant)
                                        && !lowTitle.Contains(variant.ToLower())
                                    )
                                        continue;

                                    int score = lowTitle.StartsWith(safeTitle.ToLower()) ? 100 : 80;
                                    if (spotifySec.HasValue)
                                        score -= (int)Math.Abs(dur2 - spotifySec.Value);
                                    scored.Add((score, url));
                                }
                                catch (JsonException ex)
                                {
                                    logger.Log(
                                        $"JSON parsing error for video info: {ex.Message}",
                                        true
                                    );
                                }
                            }
                        }
                    }

                    if (scored.Any())
                    {
                        return scored.OrderByDescending(x => x.score).First().url;
                    }
                }
            }
            catch (JsonException ex)
            {
                logger.Log($"JSON parsing error in deep search (step 3): {ex.Message}", true);
            }
            catch (Exception ex)
            {
                logger.Log($"Unexpected error in deep search (step 3): {ex.Message}", true);
            }
        }

        return $"ytsearch1:{query}";
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
