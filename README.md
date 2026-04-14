# Spotify2MP3.NET

A terminal user interface (TUI) application that converts Spotify playlists to local MP3 files. It parses Spotify playlist CSV exports, searches for each track on YouTube, downloads the audio, and embeds metadata.

This project is a complete rewrite of [Spotify2MP3](https://github.com/angall1/Spotify2MP3) built with C# / .NET 10 and [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui).

## Screenshots

### Main Window

![Main Window](screenshots/main.png)

### Settings Dialog

![Settings Dialog](screenshots/settings.png)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [yt-dlp](https://github.com/yt-dlp/yt-dlp)
- [FFmpeg](https://ffmpeg.org/)

## Build & Run

```bash
# Clone the repository
git clone git@github.com:pacyfist/Spotify2MP3.NET.git
cd Spotify2MP3.NET

# Run
dotnet run --project Spotify2MP3.NET/

# Or build first
dotnet build
cd Spotify2MP3.NET/bin/Debug/net10.0/

# Linux / macOS
./Spotify2MP3.NET

# Windows
./Spotify2MP3.NET.exe
```

You can optionally pass a default folder for file dialogs:

```bash
dotnet run --project Spotify2MP3.NET/ -- --folder /path/to/playlists
```

## Usage

1. **Export your Spotify playlist** as a CSV file (e.g. using [Exportify](https://exportify.net/))
2. **Launch the app** and use the Browse buttons to select your CSV file and an output folder
3. **Toggle Deep Search** for more accurate YouTube matching (slower but better results)
4. **Adjust Settings** if needed (variants, duration filters, M3U generation)
5. **Click Convert Playlist** and watch the progress in the log window

Downloaded MP3s are saved to a subfolder named after the playlist, with embedded ID3 tags (title, artist, album, track number).

### CSV Format

The app expects a Spotify CSV export with these columns:

```csv
Track Name,Artist Name(s),Album Name,Duration (ms)
```

### Settings

| Setting               | Default   | Description                                              |
| --------------------- | --------- | -------------------------------------------------------- |
| Variants              | _(empty)_ | Comma-separated search variants (e.g. `remix,acoustic`)  |
| Min Duration          | 30s       | Minimum accepted audio duration                          |
| Max Duration          | 600s      | Maximum accepted audio duration                          |
| Generate M3U          | On        | Create a `playlist.m3u` file for media players           |
| Exclude Instrumentals | Off       | Skip instrumental versions                               |
| Safe Mode             | Off       | Pace downloads to prevent YouTube throttling (see below) |

### Safe Mode

Safe Mode automatically adjusts download pacing based on playlist size to avoid YouTube rate-limiting:

| Tier       | Playlist Size | Delay Between Tracks | Rate Limit |
| ---------- | ------------- | -------------------- | ---------- |
| Normal     | < 250 tracks  | 3s                   | 5 MB/s     |
| Large      | 250–499       | 8s                   | 2 MB/s     |
| Aggressive | 500+          | 15s                  | 1 MB/s     |

Tracks that already exist on disk are skipped without any delay.

### Download Summary

After a conversion finishes, a summary dialog shows the total number of tracks processed, how many were downloaded successfully, and lists any tracks that failed.

### Output

```
output_folder/
  playlist_name/
    Artist - Track.mp3
    Artist - Track 2.mp3
    playlist.m3u
    conversion.log
```

### Keyboard Shortcuts

- `Tab` / `Shift+Tab` - Navigate between controls
- `Enter` / `Space` - Activate buttons and checkboxes
- `Ctrl+C` - Quit the application

## Running Tests

```bash
dotnet test
```

## License

This project is licensed under the [GNU Affero General Public License v3.0](LICENSE).
