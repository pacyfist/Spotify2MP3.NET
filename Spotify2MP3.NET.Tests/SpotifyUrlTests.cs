using Spotify2MP3.NET.Core;

namespace Spotify2MP3.NET.Tests;

public class SpotifyUrlTests
{
    [Theory]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M?si=abc123", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("http://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/intl-en/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/embed/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("  37i9dQZF1DXcBWIGoYBM5M  ", "37i9dQZF1DXcBWIGoYBM5M")]
    public void Parses_Valid_Inputs(string input, string expected)
    {
        Assert.True(SpotifyUrl.TryParseId(input, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/home/user/playlist.csv")]
    [InlineData("playlist.csv")]
    [InlineData("https://example.com/playlist/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/track/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/album/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:track:37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("not-a-valid-id")]
    [InlineData("37i9dQZF1DXcBWIGoYBM5")]
    [InlineData("37i9dQZF1DXcBWIGoYBM5MX")]
    public void Rejects_Invalid_Inputs(string? input)
    {
        Assert.False(SpotifyUrl.TryParseId(input, out var id));
        Assert.Equal(string.Empty, id);
    }
}
