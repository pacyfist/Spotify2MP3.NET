using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Spotify2MP3.NET.Models;

namespace Spotify2MP3.NET.Core;

public sealed record SpotifyPlaylist(string Name, List<Track> Tracks);

public sealed class SpotifyEmbedFetcher : IDisposable
{
    private static readonly Regex NextDataPattern = new(
        @"<script id=""__NEXT_DATA__""[^>]*>(.*?)</script>",
        RegexOptions.Compiled | RegexOptions.Singleline
    );

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public SpotifyEmbedFetcher(HttpClient? http = null)
    {
        if (http is null)
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            );
            _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
            _ownsClient = true;
        }
        else
        {
            _http = http;
            _ownsClient = false;
        }
    }

    public async Task<SpotifyPlaylist> FetchAsync(
        SpotifyEntityType type,
        string id,
        CancellationToken ct = default
    )
    {
        var path = type switch
        {
            SpotifyEntityType.Playlist => "playlist",
            SpotifyEntityType.Album => "album",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        var url = $"https://open.spotify.com/embed/{path}/{id}";
        var html = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        return ParseHtml(html, type);
    }

    /// <summary>
    /// Downloads raw image bytes from <paramref name="url"/>. Returns an empty array on
    /// any HTTP error so callers can treat cover-art fetch as best-effort.
    /// </summary>
    public async Task<byte[]> FetchImageBytesAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return [];
        try
        {
            return await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>
    /// Fetches the cover-art URL for a single Spotify track ID via the public embed page.
    /// Used to resolve per-track album covers for playlist inputs (where the playlist embed
    /// only carries a single playlist-level cover, not per-track album art).
    /// Returns an empty string if the URL cannot be resolved.
    /// </summary>
    public async Task<string> FetchTrackCoverArtUrlAsync(
        string trackId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return string.Empty;

        var url = $"https://open.spotify.com/embed/track/{trackId}";
        try
        {
            var html = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            var match = NextDataPattern.Match(html);
            if (!match.Success)
                return string.Empty;
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            return TryFindEntity(doc.RootElement, out var entity)
                ? ReadCoverArtUrl(entity)
                : string.Empty;
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

    public static SpotifyPlaylist ParseHtml(string html, SpotifyEntityType type)
    {
        var match = NextDataPattern.Match(html);
        if (!match.Success)
            throw new InvalidOperationException(
                "Could not find __NEXT_DATA__ in the Spotify embed page. The page format may have changed."
            );

        using var doc = JsonDocument.Parse(match.Groups[1].Value);

        if (!TryFindEntity(doc.RootElement, out var entity))
            throw new InvalidOperationException(
                "Spotify embed JSON did not contain an entity at any known path."
            );

        var name = ReadString(entity, "title");
        if (string.IsNullOrWhiteSpace(name))
            name = type == SpotifyEntityType.Album ? "album" : "playlist";

        var albumNameForTracks = type == SpotifyEntityType.Album ? name : string.Empty;

        // For album inputs the entity-level cover IS the album cover and applies to every
        // track. For playlist inputs the entity-level cover is the playlist's cover, not
        // per-track album art — leave AlbumArtUrl empty and let the caller resolve per
        // track via FetchTrackCoverArtUrlAsync(SpotifyTrackId).
        var entityCoverUrl = ReadCoverArtUrl(entity);
        var albumArtUrlForTracks =
            type == SpotifyEntityType.Album ? entityCoverUrl : string.Empty;

        var tracks = new List<Track>();
        if (
            entity.TryGetProperty("trackList", out var list)
            && list.ValueKind == JsonValueKind.Array
        )
        {
            var trackNumber = 1;
            foreach (var item in list.EnumerateArray())
            {
                var trackName = ReadString(item, "title");
                if (string.IsNullOrWhiteSpace(trackName))
                    continue;

                tracks.Add(
                    new Track
                    {
                        TrackName = trackName,
                        ArtistNames = ReadString(item, "subtitle"),
                        AlbumName = albumNameForTracks,
                        DurationMs = ReadDurationMs(item),
                        TrackNumber = trackNumber++,
                        SpotifyTrackId = ParseTrackIdFromUri(ReadString(item, "uri")),
                        AlbumArtUrl = albumArtUrlForTracks,
                    }
                );
            }
        }

        return new SpotifyPlaylist(name, tracks);
    }

    private static string ReadCoverArtUrl(JsonElement entity)
    {
        if (
            entity.TryGetProperty("coverArt", out var coverArt)
            && coverArt.TryGetProperty("sources", out var sources)
            && sources.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var source in sources.EnumerateArray())
            {
                var url = ReadString(source, "url");
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
        }
        return string.Empty;
    }

    private static string ParseTrackIdFromUri(string uri)
    {
        // Spotify URIs look like "spotify:track:<22-char-id>"
        const string prefix = "spotify:track:";
        return uri.StartsWith(prefix, StringComparison.Ordinal) ? uri[prefix.Length..] : string.Empty;
    }

    private static bool TryFindEntity(JsonElement root, out JsonElement entity)
    {
        if (TryWalk(root, out entity, "props", "pageProps", "state", "data", "entity"))
            return true;
        if (TryWalk(root, out entity, "props", "pageProps", "data", "entity"))
            return true;
        return false;
    }

    private static bool TryWalk(JsonElement element, out JsonElement result, params string[] path)
    {
        var current = element;
        foreach (var key in path)
        {
            if (
                current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(key, out current)
            )
            {
                result = default;
                return false;
            }
        }
        result = current;
        return true;
    }

    private static string ReadString(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string ReadDurationMs(JsonElement obj)
    {
        if (
            obj.TryGetProperty("duration", out var v)
            && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt64(out var ms)
        )
            return ms.ToString();
        return string.Empty;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
