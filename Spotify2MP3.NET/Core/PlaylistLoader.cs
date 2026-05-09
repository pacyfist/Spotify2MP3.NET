using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Spotify2MP3.NET.Models;

namespace Spotify2MP3.NET.Core;

public static class PlaylistLoader
{
    public static async Task<(List<Track> Tracks, string PlaylistName)> LoadFromSpotifyAsync(
        SpotifyEntityType type,
        string id,
        CancellationToken ct
    )
    {
        using var fetcher = new SpotifyEmbedFetcher();
        var playlist = await fetcher.FetchAsync(type, id, ct);
        return (playlist.Tracks, SanitizeFolderName(playlist.Name));
    }

    public static (List<Track> Tracks, string PlaylistName) LoadFromCsv(string path)
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

    public static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        var cleaned = sb.ToString().Trim();
        return string.IsNullOrEmpty(cleaned) ? "playlist" : cleaned;
    }
}
