using TagLib;

namespace Spotify2MP3.NET.Core;

public class MetadataEmbedder
{
    public void EmbedTags(
        string filePath,
        string title,
        string artist,
        string album,
        uint trackNumber,
        byte[]? coverImage = null
    )
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            file.Tag.Title = title;
            file.Tag.Performers = [artist];
            file.Tag.Album = album;
            file.Tag.Track = trackNumber;

            if (coverImage is { Length: > 0 })
            {
                file.Tag.Pictures =
                [
                    new Picture(new ByteVector(coverImage))
                    {
                        Type = PictureType.FrontCover,
                        MimeType = "image/jpeg",
                        Description = "Cover",
                    },
                ];
            }

            file.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error embedding metadata: {ex.Message}");
        }
    }
}
