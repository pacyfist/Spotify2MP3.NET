using System;
using System.Linq;
using Spotify2Media.Models;
using Terminal.Gui;

namespace Spotify2Media.UI;

public class SettingsDialog : Dialog
{
    public SettingsDialog(Config config)
        : base()
    {
        Title = "Settings";
        Width = 60;
        Height = 15;
        ColorScheme = Colors.ColorSchemes["Dialog"];
        var y = 1;
        Add(
            new Label()
            {
                Text = "Variants (comma separated):",
                X = 1,
                Y = y,
            }
        );
        var variantsField = new TextField()
        {
            Text = string.Join(",", config.Variants),
            X = 30,
            Y = y++,
            Width = 20,
        };
        Add(variantsField);

        Add(
            new Label()
            {
                Text = "Min Duration (s):",
                X = 1,
                Y = y,
            }
        );
        var minField = new TextField()
        {
            Text = config.DurationMin.ToString(),
            X = 30,
            Y = y++,
            Width = 10,
        };
        Add(minField);

        Add(
            new Label()
            {
                Text = "Max Duration (s):",
                X = 1,
                Y = y,
            }
        );
        var maxField = new TextField()
        {
            Text = config.DurationMax.ToString(),
            X = 30,
            Y = y++,
            Width = 10,
        };
        Add(maxField);

        var m3uCheck = new CheckBox()
        {
            Text = "Generate M3U",
            X = 1,
            Y = y++,
            CheckedState = config.GenerateM3u ? CheckState.Checked : CheckState.UnChecked,
        };
        var instrCheck = new CheckBox()
        {
            Text = "Exclude Instrumentals",
            X = 1,
            Y = y++,
            CheckedState = config.ExcludeInstrumentals ? CheckState.Checked : CheckState.UnChecked,
        };
        Add(m3uCheck, instrCheck);

        var saveBtn = new Button()
        {
            Text = "Save",
            IsDefault = true,
            X = Pos.Center() - 10,
            Y = y + 1,
        };
        var cancelBtn = new Button()
        {
            Text = "Cancel",
            X = Pos.Center() + 2,
            Y = y + 1,
        };

        saveBtn.Accepting += (s, e) =>
        {
            config.Variants = (variantsField.Text?.ToString() ?? "")
                .Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
            if (int.TryParse(minField.Text.ToString(), out int min))
                config.DurationMin = min;
            if (int.TryParse(maxField.Text.ToString(), out int max))
                config.DurationMax = max;
            config.GenerateM3u = m3uCheck.CheckedState == CheckState.Checked;
            config.ExcludeInstrumentals = instrCheck.CheckedState == CheckState.Checked;
            Application.RequestStop();
        };

        cancelBtn.Accepting += (s, e) => Application.RequestStop();

        Add(saveBtn, cancelBtn);
    }
}
