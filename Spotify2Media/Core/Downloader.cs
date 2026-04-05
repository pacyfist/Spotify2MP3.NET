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
    Action<string, bool> logCallback
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
        string M4aPath,
        string Mp3Path,
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

                // Step 2: Check if files exist
                logCallback($"Processing: {variantInfo.BaseName}", false);
                bool m4aExists = IsM4aPresent(variantInfo);

                if (m4aExists)
                {
                    logCallback("[OK] m4a already exists.", false);
                    success = true;
                }
                else
                {
                    // Step 3: Search for the track
                    statusCallback($"[{i}/{total}] Processing: {variantInfo.Query}");
                    logCallback($"Searching for: {variantInfo.Query}", false);

                    string downloadSpec = $"ytsearch1:{variantInfo.Query}";

                    if (deepSearch)
                    {
                        logCallback("Performing deep search...", false);
                        downloadSpec = await PerformDeepSearchAsync(
                                variantInfo.Query,
                                variantInfo.SafeTitle,
                                variantInfo.SafeArtist,
                                variantInfo.Variant,
                                variantInfo.SpotifySec,
                                ct
                            )
                            .ConfigureAwait(false);
                        logCallback($"Deep search result: {downloadSpec}", false);
                    }

                    // Step 4: Download the track (if m4a is missing)
                    bool downloaded = await DownloadTrackAsync(variantInfo, downloadSpec, ct)
                        .ConfigureAwait(false);

                    if (downloaded)
                    {
                        logCallback("[OK] m4a download successful.", false);
                        success = true;
                    }
                    else
                    {
                        logCallback("[Error] m4a download failed", true);
                        success = false;
                        break;
                    }
                }

                if (config.TranscodeMp3)
                {
                    bool mp3Exists = IsMp3Present(variantInfo);

                    if (mp3Exists)
                    {
                        logCallback("[OK] mp3 already exists.", false);
                        success = true;
                    }
                    else
                    {
                        // Step 5: Convert the track
                        bool converted = await ConvertTrackAsync(variantInfo, track, ct)
                            .ConfigureAwait(false);

                        if (converted)
                        {
                            logCallback("[OK] mp3 conversion successful.", false);
                            success = true;
                        }
                        else
                        {
                            logCallback("[Error] mp3 conversion failed", true);
                            success = false;
                            break;
                        }
                    }
                }
            }

            if (!success)
            {
                notFound.Add(track);
                logCallback($"Failed to find or download: {track.TrackName}", true);
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
        var m4aPath = Path.Combine(outputDir, baseName + ".m4a");
        var mp3Path = Path.Combine(outputDir, baseName + ".mp3");

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
            m4aPath,
            mp3Path,
            query,
            trackInfo.SafeTitle,
            trackInfo.SafeArtist,
            trackInfo.ArtistPrimary,
            variant,
            trackInfo.SpotifySec
        );
    }

    private bool IsM4aPresent(VariantInfo variantInfo) => File.Exists(variantInfo.M4aPath);

    private bool IsMp3Present(VariantInfo variantInfo) => File.Exists(variantInfo.Mp3Path);

    private async Task<bool> DownloadTrackAsync(
        VariantInfo variantInfo,
        string downloadSpec,
        CancellationToken ct
    )
    {
        var tmpl = variantInfo.BaseName + ".%(ext)s";
        var args = new List<string>
        {
            "--no-config",
            "-f",
            "bestaudio[ext=m4a]/bestaudio",
            "--output",
            Path.Combine(outputDir, tmpl),
            "--no-playlist",
            "--embed-thumbnail",
            "--add-metadata",
            "--remux-video",
            "m4a",
        };

        if (config.ExcludeInstrumentals)
        {
            args.AddRange(new[] { "--reject-title", "instrumental" });
        }

        args.Add(downloadSpec);

        logCallback(
            $"yt-dlp {string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a))}",
            false
        );

        var (exitCode, stdout, stderr) = await RunCommandAsync("yt-dlp", args, ct)
            .ConfigureAwait(false);

        if (exitCode == 0 || stdout.Contains("has already been recorded in the archive"))
        {
            if (File.Exists(variantInfo.M4aPath))
            {
                logCallback(
                    $"Successfully downloaded: {Path.GetFileName(variantInfo.M4aPath)}",
                    false
                );
                return true;
            }
            else
            {
                logCallback($"File not found after download: {variantInfo.M4aPath}", true);
            }
        }
        else
        {
            logCallback($"yt-dlp download failed: {stderr}", true);
        }

        return false;
    }

    private async Task<bool> ConvertTrackAsync(
        VariantInfo variantInfo,
        Track track,
        CancellationToken ct
    )
    {
        if (config.TranscodeMp3)
        {
            if (!File.Exists(variantInfo.Mp3Path))
            {
                logCallback(
                    $"Conversion to MP3 enabled. Transcoding {Path.GetFileName(variantInfo.M4aPath)} to MP3...",
                    false
                );
                var args = new[]
                {
                    "-y",
                    "-i",
                    variantInfo.M4aPath,
                    "-q:a",
                    "0",
                    variantInfo.Mp3Path,
                };
                var fullCmd = $"ffmpeg {string.Join(" ", args)}";
                logCallback($"Executing command: {fullCmd}", false);

                var (exitCode, stdout, stderr) = await RunCommandAsync("ffmpeg", args, ct)
                    .ConfigureAwait(false);

                if (exitCode != 0 || !File.Exists(variantInfo.Mp3Path))
                {
                    logCallback($"ffmpeg conversion failed: {stderr}", true);
                    return false;
                }
                logCallback(
                    $"Successfully converted to MP3: {Path.GetFileName(variantInfo.Mp3Path)}",
                    false
                );
            }
            else
            {
                logCallback(
                    $"MP3 already exists, skipping conversion: {Path.GetFileName(variantInfo.Mp3Path)}",
                    false
                );
            }
        }

        var finalPath = config.TranscodeMp3 ? variantInfo.Mp3Path : variantInfo.M4aPath;
        var metadata = new MetadataEmbedder();
        logCallback("Embedding metadata...", false);
        metadata.EmbedTags(
            finalPath,
            track.TrackName ?? "Unknown",
            variantInfo.ArtistPrimary,
            track.AlbumName ?? "Unknown",
            (uint)track.TrackNumber,
            config.TranscodeMp3
        );
        logCallback($"Successfully finished processing: {variantInfo.BaseName}", false);

        return true;
    }

    private void GenerateM3uPlaylist()
    {
        var m3uPath = Path.Combine(outputDir, "playlist.m3u");
        var audioFiles = Directory
            .GetFiles(outputDir)
            .Where(f => f.EndsWith(".mp3") || f.EndsWith(".m4a"))
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
                logCallback($"JSON parsing error in deep search (step 1): {ex.Message}", true);
            }
            catch (Exception ex)
            {
                logCallback($"Unexpected error in deep search (step 1): {ex.Message}", true);
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
                                    logCallback(
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
                logCallback($"JSON parsing error in deep search (step 3): {ex.Message}", true);
            }
            catch (Exception ex)
            {
                logCallback($"Unexpected error in deep search (step 3): {ex.Message}", true);
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
