using Spotify2MP3.NET.Core;

namespace Spotify2MP3.NET.Tests;

public class SpotifyUrlTests
{
    [Theory]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M?si=abc123", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("http://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/intl-en/playlist/37i9dQZF1DXcBWIGoYBM5M", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/embed/playlist/37i9dQZF1DXcBWIGoYBM5M", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/album/5AFf68gtvNgGLZarsyEEL8", SpotifyEntityType.Album, "5AFf68gtvNgGLZarsyEEL8")]
    [InlineData("https://open.spotify.com/album/5AFf68gtvNgGLZarsyEEL8?si=EeWsu17HReSDbYOru8dX3Q", SpotifyEntityType.Album, "5AFf68gtvNgGLZarsyEEL8")]
    [InlineData("https://open.spotify.com/intl-en/album/5AFf68gtvNgGLZarsyEEL8", SpotifyEntityType.Album, "5AFf68gtvNgGLZarsyEEL8")]
    [InlineData("https://open.spotify.com/embed/album/5AFf68gtvNgGLZarsyEEL8", SpotifyEntityType.Album, "5AFf68gtvNgGLZarsyEEL8")]
    [InlineData("spotify:album:5AFf68gtvNgGLZarsyEEL8", SpotifyEntityType.Album, "5AFf68gtvNgGLZarsyEEL8")]
    [InlineData("37i9dQZF1DXcBWIGoYBM5M", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("  37i9dQZF1DXcBWIGoYBM5M  ", SpotifyEntityType.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    public void Parses_Valid_Inputs(string input, SpotifyEntityType expectedType, string expectedId)
    {
        Assert.True(SpotifyUrl.TryParse(input, out var type, out var id));
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/home/user/playlist.csv")]
    [InlineData("playlist.csv")]
    [InlineData("https://example.com/playlist/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/track/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/artist/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:track:37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:artist:37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("not-a-valid-id")]
    [InlineData("37i9dQZF1DXcBWIGoYBM5")]
    [InlineData("37i9dQZF1DXcBWIGoYBM5MX")]
    public void Rejects_Invalid_Inputs(string? input)
    {
        Assert.False(SpotifyUrl.TryParse(input, out _, out var id));
        Assert.Equal(string.Empty, id);
    }
}
