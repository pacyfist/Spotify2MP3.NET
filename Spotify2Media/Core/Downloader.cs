using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Spotify2Media.Models;

namespace Spotify2Media.Core;

public class Downloader
{
    private readonly Config _config;
    private readonly string _outputDir;
    private readonly Action<string> _statusCallback;
    private readonly Action<int> _progressCallback;
    private readonly bool _deepSearch;

    private readonly Action<string, bool> _logCallback;

    public Downloader(Config config, string outputDir, bool deepSearch, Action<string> statusCallback, Action<int> progressCallback, Action<string, bool> logCallback)
    {
        _config = config;
        _outputDir = outputDir;
        _deepSearch = deepSearch;
        _statusCallback = statusCallback;
        _progressCallback = progressCallback;
        _logCallback = logCallback;
    }

    public async Task<List<Track>> DownloadPlaylistAsync(List<Track> tracks)
    {
        var notFound = new List<Track>();
        int total = tracks.Count;
        int i = 1;

        Directory.CreateDirectory(_outputDir);
        var archiveFile = Path.Combine(_outputDir, "downloaded.txt");

        foreach (var track in tracks)
        {
            track.TrackNumber = i;
            var safeTitle = Regex.Replace(track.TrackName ?? "", @"[^\w\s]", "");
            var artistRaw = track.ArtistNames ?? "";
            var artistPrimary = Regex.Split(artistRaw, @"[,/&]| feat\.| ft\.", RegexOptions.IgnoreCase)[0].Trim();
            var safeArtist = Regex.Replace(artistPrimary, @"[^\w\s]", "");

            int.TryParse(track.DurationMs, out int spotifyMs);
            int? spotifySec = spotifyMs > 0 ? spotifyMs / 1000 : null;

            var variants = _config.Variants.ToList();
            if (variants.Count == 0) variants.Add("");
            if ((track.TrackName ?? "").ToLower().Contains("instrumental"))
            {
                variants.Insert(0, "instrumental");
            }

            bool found = false;

            foreach (var variant in variants)
            {
                var parts = new List<string> { safeTitle };
                if (!string.IsNullOrWhiteSpace(safeArtist) && safeArtist.ToLower() != "unknown") parts.Add(safeArtist);
                if (!string.IsNullOrWhiteSpace(variant)) parts.Add(variant);

                var query = string.Join(" ", parts);
                _statusCallback($"[{i}/{total}] Searching: {query}");

                string downloadSpec = $"ytsearch1:{query}";

                if (_deepSearch)
                {
                    downloadSpec = await PerformDeepSearchAsync(query, safeTitle, safeArtist, variant, spotifySec);
                }

                var baseName = $"{i:D3} - {Regex.Replace(track.TrackName ?? "", @"[^\w\s]", "").Trim()}{(string.IsNullOrEmpty(variant) ? "" : $" - {variant}")}";
                var tmpl = baseName + ".%(ext)s";
                
                var args = new List<string>
                {
                    "--no-config",
                    "--download-archive", archiveFile,
                    "-f", "bestaudio[ext=m4a]/bestaudio",
                    "--output", Path.Combine(_outputDir, tmpl),
                    "--no-playlist",
                    "--embed-thumbnail",
                    "--add-metadata"
                };

                if (_config.TranscodeMp3)
                {
                    args.AddRange(new[] { "--extract-audio", "--audio-format", "mp3", "--audio-quality", "0" });
                }
                else
                {
                    args.AddRange(new[] { "--remux-video", "m4a" });
                }

                if (_config.ExcludeInstrumentals)
                {
                    args.AddRange(new[] { "--reject-title", "instrumental" });
                }

                args.Add(downloadSpec);

                var (exitCode, stdout, stderr) = await RunCommandAsync("yt-dlp", args);

                if (exitCode == 0 || stdout.Contains("has already been recorded in the archive"))
                {
                    var outExt = _config.TranscodeMp3 ? ".mp3" : ".m4a";
                    var candidatePath = Path.Combine(_outputDir, baseName + outExt);
                    
                    if (File.Exists(candidatePath))
                    {
                        var metadata = new MetadataEmbedder();
                        metadata.EmbedTags(candidatePath, track.TrackName ?? "Unknown", artistPrimary, track.AlbumName ?? "Unknown", (uint)i, outExt == ".mp3");
                        found = true;
                        _logCallback($"Downloaded: {baseName}", false);
                        break;
                    }
                    else
                    {
                        _logCallback($"File not found after download: {candidatePath}", true);
                    }
                }
                else
                {
                    _logCallback($"Download failed for {query}: {stderr}", true);
                }
            }

            if (!found)
            {
                notFound.Add(track);
                _logCallback($"Failed to find or download: {track.TrackName}", true);
            }

            _progressCallback((int)((double)i / total * 100));
            i++;
        }

        if (_config.GenerateM3u)
        {
            var m3uPath = Path.Combine(_outputDir, "playlist.m3u");
            var audioFiles = Directory.GetFiles(_outputDir).Where(f => f.EndsWith(".mp3") || f.EndsWith(".m4a")).OrderBy(File.GetCreationTime).ToList();
            using var sw = new StreamWriter(m3uPath);
            sw.WriteLine("#EXTM3U");
            foreach (var file in audioFiles)
            {
                sw.WriteLine($"#EXTINF:-1,{Path.GetFileNameWithoutExtension(file)}");
                sw.WriteLine(Path.GetFileName(file));
            }
        }

        return notFound;
    }

    private async Task<string> PerformDeepSearchAsync(string query, string safeTitle, string safeArtist, string variant, int? spotifySec)
    {
        var (code1, stdout1, _) = await RunCommandAsync("yt-dlp", new[] { "--dump-single-json", "--no-playlist", $"ytsearch1:{query}" });
        if (code1 == 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(stdout1);
                var root = doc.RootElement;
                if (root.TryGetProperty("entries", out var entries) && entries.GetArrayLength() > 0)
                {
                    var top = entries[0];
                    var vidTitle = top.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var uploader = top.TryGetProperty("uploader", out var u) ? (u.GetString() ?? "").ToLower() : "";
                    var duration = top.TryGetProperty("duration", out var d) ? d.GetDouble() : 0;

                    bool passes = safeTitle.ToLower().Contains(vidTitle.ToLower()) || vidTitle.ToLower().Contains(safeTitle.ToLower());
                    if (!string.IsNullOrEmpty(safeArtist)) passes &= uploader.Contains(safeArtist.ToLower());
                    if (spotifySec.HasValue) passes &= Math.Abs(duration - spotifySec.Value) <= 10;
                    passes &= duration >= _config.DurationMin && duration <= _config.DurationMax;

                    if (passes && top.TryGetProperty("webpage_url", out var url))
                    {
                        return url.GetString() ?? $"ytsearch1:{query}";
                    }
                }
            }
            catch { }
        }

        var (code3, stdout3, _) = await RunCommandAsync("yt-dlp", new[] { "--flat-playlist", "--dump-single-json", "--no-playlist", $"ytsearch3:{query}" });
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
                            var (codeI, stdoutI, _) = await RunCommandAsync("yt-dlp", new[] { "--dump-single-json", "--no-playlist", url });
                            if (codeI == 0)
                            {
                                try
                                {
                                    using var iDoc = JsonDocument.Parse(stdoutI);
                                    var info = iDoc.RootElement;
                                    var rawTitle = info.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                    var lowTitle = rawTitle.ToLower();
                                    var dur2 = info.TryGetProperty("duration", out var d) ? d.GetDouble() : 0;
                                    var up2 = info.TryGetProperty("uploader", out var u) ? (u.GetString() ?? "").ToLower() : "";

                                    if (dur2 < _config.DurationMin || dur2 > _config.DurationMax) continue;
                                    if (lowTitle.Contains("#shorts")) continue;
                                    if (!string.IsNullOrEmpty(safeArtist) && !up2.Contains(safeArtist.ToLower())) continue;
                                    if (!string.IsNullOrEmpty(variant) && !lowTitle.Contains(variant.ToLower())) continue;

                                    int score = lowTitle.StartsWith(safeTitle.ToLower()) ? 100 : 80;
                                    if (spotifySec.HasValue) score -= (int)Math.Abs(dur2 - spotifySec.Value);
                                    scored.Add((score, url));
                                }
                                catch { }
                            }
                        }
                    }

                    if (scored.Any())
                    {
                        return scored.OrderByDescending(x => x.score).First().url;
                    }
                }
            }
            catch { }
        }

        return $"ytsearch1:{query}";
    }

    private async Task<(int exitCode, string stdout, string stderr)> RunCommandAsync(string command, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            return (process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (Exception ex)
        {
            return (-1, "", $"Failed to start {command}: {ex.Message}");
        }
    }
}
