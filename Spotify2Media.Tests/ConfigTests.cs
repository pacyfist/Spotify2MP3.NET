using System.IO;
using Xunit;
using Spotify2Media.Models;

namespace Spotify2Media.Tests;

public class ConfigTests
{
    [Fact]
    public void Can_Save_And_Load_Config()
    {
        var config = new Config
        {
            DurationMin = 45,
            TranscodeMp3 = true
        };
        config.Save();

        var loaded = Config.Load();

        Assert.Equal(45, loaded.DurationMin);
        Assert.True(loaded.TranscodeMp3);
        Assert.False(loaded.ExcludeInstrumentals);
    }
}
