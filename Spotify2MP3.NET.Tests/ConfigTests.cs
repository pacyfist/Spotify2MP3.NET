using System.IO;
using Xunit;
using Spotify2MP3.NET.Models;

namespace Spotify2MP3.NET.Tests;

public class ConfigTests
{
    [Fact]
    public void Can_Save_And_Load_Config()
    {
        var config = new Config
        {
            DurationMin = 45,
            Exclude = ["instrumental", "karaoke"]
        };
        config.Save();

        var loaded = Config.Load();

        Assert.Equal(45, loaded.DurationMin);
        Assert.Equal(new[] { "instrumental", "karaoke" }, loaded.Exclude);
        Assert.True(loaded.GenerateM3u);
    }
}
