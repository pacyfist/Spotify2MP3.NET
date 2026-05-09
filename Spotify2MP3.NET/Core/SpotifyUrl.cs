using System;
using System.Text.RegularExpressions;

namespace Spotify2MP3.NET.Core;

public static class SpotifyUrl
{
    private static readonly Regex IdPattern = new(
        "^[A-Za-z0-9]{22}$",
        RegexOptions.Compiled
    );

    public static bool TryParseId(string? input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var s = input.Trim();

        const string uriPrefix = "spotify:playlist:";
        if (s.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase))
            return TrySetId(s[uriPrefix.Length..], out id);

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
                if (segments[i].Equals("playlist", StringComparison.OrdinalIgnoreCase))
                    return TrySetId(segments[i + 1], out id);
            }
            return false;
        }

        return TrySetId(s, out id);
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
