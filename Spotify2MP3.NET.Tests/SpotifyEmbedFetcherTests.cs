using Spotify2MP3.NET.Core;

namespace Spotify2MP3.NET.Tests;

public class SpotifyEmbedFetcherTests
{
    [Fact]
    public void Parses_Tracks_From_Playlist_Embed_Json()
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

        var playlist = SpotifyEmbedFetcher.ParseHtml(html, SpotifyEntityType.Playlist);

        Assert.Equal("My Playlist", playlist.Name);
        Assert.Equal(2, playlist.Tracks.Count);

        Assert.Equal("Song A", playlist.Tracks[0].TrackName);
        Assert.Equal("Artist 1", playlist.Tracks[0].ArtistNames);
        Assert.Equal(string.Empty, playlist.Tracks[0].AlbumName);
        Assert.Equal("180000", playlist.Tracks[0].DurationMs);
        Assert.Equal(1, playlist.Tracks[0].TrackNumber);

        Assert.Equal("Song B", playlist.Tracks[1].TrackName);
        Assert.Equal(2, playlist.Tracks[1].TrackNumber);
    }

    [Fact]
    public void Album_Tracks_Get_Album_Name_From_Entity()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"state":{"data":{"entity":{
              "type": "album",
              "title": "Greatest Hits",
              "subtitle": "Some Artist",
              "trackList": [
                {"title": "Track One", "subtitle": "Some Artist", "duration": 200000},
                {"title": "Track Two", "subtitle": "Some Artist", "duration": 210000}
              ]
            }}}}}}
            </script>
            """;

        var album = SpotifyEmbedFetcher.ParseHtml(html, SpotifyEntityType.Album);

        Assert.Equal("Greatest Hits", album.Name);
        Assert.Equal(2, album.Tracks.Count);
        Assert.All(album.Tracks, t => Assert.Equal("Greatest Hits", t.AlbumName));
        Assert.Equal("Track One", album.Tracks[0].TrackName);
        Assert.Equal(1, album.Tracks[0].TrackNumber);
        Assert.Equal(2, album.Tracks[1].TrackNumber);
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

        var playlist = SpotifyEmbedFetcher.ParseHtml(html, SpotifyEntityType.Playlist);

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

        var playlist = SpotifyEmbedFetcher.ParseHtml(html, SpotifyEntityType.Playlist);
        Assert.Equal("playlist", playlist.Name);

        var album = SpotifyEmbedFetcher.ParseHtml(html, SpotifyEntityType.Album);
        Assert.Equal("album", album.Name);
    }

    [Fact]
    public void Throws_When_NextData_Missing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SpotifyEmbedFetcher.ParseHtml(
                "<html><body>no script</body></html>",
                SpotifyEntityType.Playlist
            )
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

        Assert.Throws<InvalidOperationException>(() =>
            SpotifyEmbedFetcher.ParseHtml(html, SpotifyEntityType.Playlist)
        );
    }
}
