using System;
using TagLib;

namespace Spotify2Media.Core;

public class MetadataEmbedder
{
    public void EmbedTags(string filePath, string title, string artist, string album, uint trackNumber, bool isMp3)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            file.Tag.Title = title;
            file.Tag.Performers = new[] { artist };
            file.Tag.Album = album;
            file.Tag.Track = trackNumber;
            file.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error embedding metadata: {ex.Message}");
        }
    }
}
