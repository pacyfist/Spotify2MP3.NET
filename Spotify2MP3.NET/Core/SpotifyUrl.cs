using System;
using System.Text.RegularExpressions;

namespace Spotify2MP3.NET.Core;

public enum SpotifyEntityType
{
    Playlist,
    Album,
}

public static class SpotifyUrl
{
    private static readonly Regex IdPattern = new(
        "^[A-Za-z0-9]{22}$",
        RegexOptions.Compiled
    );

    public static bool TryParse(string? input, out SpotifyEntityType type, out string id)
    {
        type = SpotifyEntityType.Playlist;
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var s = input.Trim();

        if (s.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = s.Split(':');
            if (parts.Length != 3)
                return false;
            if (!TryParseEntityType(parts[1], out type))
                return false;
            return TrySetId(parts[2], out id);
        }

        if (
            s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
                return false;
            if (!uri.Host.Equals("open.spotify.com", StringComparison.OrdinalIgnoreCase))
                return false;

            var segments = uri.AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            );
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (TryParseEntityType(segments[i], out type))
                    return TrySetId(segments[i + 1], out id);
            }
            return false;
        }

        type = SpotifyEntityType.Playlist;
        return TrySetId(s, out id);
    }

    private static bool TryParseEntityType(string token, out SpotifyEntityType type)
    {
        if (token.Equals("playlist", StringComparison.OrdinalIgnoreCase))
        {
            type = SpotifyEntityType.Playlist;
            return true;
        }
        if (token.Equals("album", StringComparison.OrdinalIgnoreCase))
        {
            type = SpotifyEntityType.Album;
            return true;
        }
        type = default;
        return false;
    }

    private static bool TrySetId(string candidate, out string id)
    {
        if (IdPattern.IsMatch(candidate))
        {
            id = candidate;
            return true;
        }
        id = string.Empty;
        return false;
    }
}
