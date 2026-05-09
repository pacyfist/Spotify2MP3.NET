using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Collections.Generic;
using System;

namespace Spotify2MP3.NET.Models;

public class Config
{
    [JsonPropertyName("variants")]
    public List<string> Variants { get; set; } = new List<string>();

    [JsonPropertyName("duration_min")]
    public int DurationMin { get; set; } = 30;

    [JsonPropertyName("duration_max")]
    public int DurationMax { get; set; } = 600;

    [JsonPropertyName("generate_m3u")]
    public bool GenerateM3u { get; set; } = true;

    [JsonPropertyName("exclude_instrumentals")]
    public bool ExcludeInstrumentals { get; set; } = false;

    [JsonPropertyName("safe_mode")]
    public bool SafeMode { get; set; } = false;

    [JsonPropertyName("use_spotify_cover_art")]
    public bool UseSpotifyCoverArt { get; set; } = false;

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public static Config Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<Config>(json) ?? new Config();
            }
            catch
            {
                return new Config();
            }
        }
        return new Config();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
