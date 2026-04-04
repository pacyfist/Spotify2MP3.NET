using System;
using System.Linq;
using Terminal.Gui;
using Spotify2Media.Models;

namespace Spotify2Media.UI;

public class SettingsDialog : Dialog
{
    public SettingsDialog(Config config) : base("Settings", 60, 15)
    {
        ColorScheme = Colors.Dialog;
        var y = 1;
        Add(new Label("Variants (comma separated):") { X = 1, Y = y });
        var variantsField = new TextField(string.Join(",", config.Variants)) { X = 30, Y = y++, Width = 20 };
        Add(variantsField);

        Add(new Label("Min Duration (s):") { X = 1, Y = y });
        var minField = new TextField(config.DurationMin.ToString()) { X = 30, Y = y++, Width = 10 };
        Add(minField);

        Add(new Label("Max Duration (s):") { X = 1, Y = y });
        var maxField = new TextField(config.DurationMax.ToString()) { X = 30, Y = y++, Width = 10 };
        Add(maxField);

        var mp3Check = new CheckBox("Transcode to MP3") { X = 1, Y = y++, Checked = config.TranscodeMp3 };
        var m3uCheck = new CheckBox("Generate M3U") { X = 1, Y = y++, Checked = config.GenerateM3u };
        var instrCheck = new CheckBox("Exclude Instrumentals") { X = 1, Y = y++, Checked = config.ExcludeInstrumentals };
        Add(mp3Check, m3uCheck, instrCheck);

        var saveBtn = new Button("Save", is_default: true) { X = Pos.Center() - 10, Y = y + 1 };
        var cancelBtn = new Button("Cancel") { X = Pos.Center() + 2, Y = y + 1 };

        saveBtn.Clicked += () => {
            config.Variants = (variantsField.Text?.ToString() ?? "").Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
            if (int.TryParse(minField.Text.ToString(), out int min)) config.DurationMin = min;
            if (int.TryParse(maxField.Text.ToString(), out int max)) config.DurationMax = max;
            config.TranscodeMp3 = mp3Check.Checked;
            config.GenerateM3u = m3uCheck.Checked;
            config.ExcludeInstrumentals = instrCheck.Checked;
            Application.RequestStop();
        };

        cancelBtn.Clicked += () => Application.RequestStop();

        Add(saveBtn, cancelBtn);
    }
}
