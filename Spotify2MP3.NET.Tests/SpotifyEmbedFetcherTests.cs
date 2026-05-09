using Spotify2MP3.NET.Core;

namespace Spotify2MP3.NET.Tests;

public class SpotifyEmbedFetcherTests
{
    [Fact]
    public void Parses_Tracks_From_Embed_Json()
    {
        const string html = """
            <html><body><script id="__NEXT_DATA__" type="application/json">
            {
              "props": {
                "pageProps": {
                  "state": {
                    "data": {
                      "entity": {
                        "type": "playlist",
                        "title": "My Playlist",
                        "trackList": [
                          { "title": "Song A", "subtitle": "Artist 1", "duration": 180000 },
                          { "title": "Song B", "subtitle": "Artist 2, Artist 3", "duration": 240000 }
                        ]
                      }
                    }
                  }
                }
              }
            }
            </script></body></html>
            """;

        var playlist = SpotifyEmbedFetcher.ParsePlaylistHtml(html);

        Assert.Equal("My Playlist", playlist.Name);
        Assert.Equal(2, playlist.Tracks.Count);

        Assert.Equal("Song A", playlist.Tracks[0].TrackName);
        Assert.Equal("Artist 1", playlist.Tracks[0].ArtistNames);
        Assert.Equal("180000", playlist.Tracks[0].DurationMs);
        Assert.Equal(1, playlist.Tracks[0].TrackNumber);

        Assert.Equal("Song B", playlist.Tracks[1].TrackName);
        Assert.Equal("Artist 2, Artist 3", playlist.Tracks[1].ArtistNames);
        Assert.Equal(2, playlist.Tracks[1].TrackNumber);
    }

    [Fact]
    public void Skips_Tracks_With_Empty_Title()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"state":{"data":{"entity":{
              "title": "P",
              "trackList": [
                {"title": "", "subtitle": "X"},
                {"title": "Real", "subtitle": "Y"}
              ]
            }}}}}}
            </script>
            """;

        var playlist = SpotifyEmbedFetcher.ParsePlaylistHtml(html);

        Assert.Single(playlist.Tracks);
        Assert.Equal("Real", playlist.Tracks[0].TrackName);
        Assert.Equal(1, playlist.Tracks[0].TrackNumber);
    }

    [Fact]
    public void Falls_Back_To_Default_Name_When_Title_Missing()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"state":{"data":{"entity":{
              "trackList": []
            }}}}}}
            </script>
            """;

        var playlist = SpotifyEmbedFetcher.ParsePlaylistHtml(html);

        Assert.Equal("playlist", playlist.Name);
        Assert.Empty(playlist.Tracks);
    }

    [Fact]
    public void Throws_When_NextData_Missing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SpotifyEmbedFetcher.ParsePlaylistHtml("<html><body>no script</body></html>")
        );
    }

    [Fact]
    public void Throws_When_Entity_Path_Missing()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{}}}
            </script>
            """;

        Assert.Throws<InvalidOperationException>(() => SpotifyEmbedFetcher.ParsePlaylistHtml(html));
    }
}
