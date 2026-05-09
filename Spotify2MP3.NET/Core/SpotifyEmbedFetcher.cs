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
                    }
                );
            }
        }

        return new SpotifyPlaylist(name, tracks);
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
