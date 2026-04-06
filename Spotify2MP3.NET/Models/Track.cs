using CsvHelper.Configuration.Attributes;

namespace Spotify2MP3.NET.Models;

public class Track
{
    [Name("Track Name", "Track name")]
    public string TrackName { get; set; } = string.Empty;

    [Name("Artist Name(s)", "Artist name")]
    public string ArtistNames { get; set; } = string.Empty;

    [Name("Album Name", "Album")]
    public string AlbumName { get; set; } = string.Empty;

    [Name("Duration (ms)")]
    [Optional]
    public string DurationMs { get; set; } = string.Empty;

    [Ignore]
    public int TrackNumber { get; set; }
}
