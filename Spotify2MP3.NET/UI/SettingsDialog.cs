using Spotify2MP3.NET.Models;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Spotify2MP3.NET.UI;

public class SettingsDialog : Dialog
{
    public SettingsDialog(Config config)
    {
        Title = "Settings";
        SchemeName = "Dialog";
        Width = 60;
        Height = 17;

        var variantsLabel = new Label
        {
            Text = "Variants (comma separated):",
            X = 1,
            Y = 1,
        };
        var variantsField = new TextField
        {
            Text = string.Join(",", config.Variants),
            X = Pos.Right(variantsLabel) + 1,
            Y = Pos.Top(variantsLabel),
            Width = Dim.Fill(1),
        };

        var minLabel = new Label
        {
            Text = "Min Duration (s):",
            X = 1,
            Y = Pos.Bottom(variantsLabel),
        };
        var minField = new TextField
        {
            Text = config.DurationMin.ToString(),
            X = Pos.Right(variantsLabel) + 1,
            Y = Pos.Top(minLabel),
            Width = 10,
        };

        var maxLabel = new Label
        {
            Text = "Max Duration (s):",
            X = 1,
            Y = Pos.Bottom(minLabel),
        };
        var maxField = new TextField
        {
            Text = config.DurationMax.ToString(),
            X = Pos.Right(variantsLabel) + 1,
            Y = Pos.Top(maxLabel),
            Width = 10,
        };

        var m3uCheck = new CheckBox
        {
            Text = "Generate _M3U",
            X = 1,
            Y = Pos.Bottom(maxLabel) + 1,
            Value = config.GenerateM3u ? CheckState.Checked : CheckState.UnChecked,
        };
        var instrCheck = new CheckBox
        {
            Text = "Exclude _Instrumentals",
            X = 1,
            Y = Pos.Bottom(m3uCheck),
            Value = config.ExcludeInstrumentals ? CheckState.Checked : CheckState.UnChecked,
        };
        var safeCheck = new CheckBox
        {
            Text = "Sa_fe Mode (pace downloads)",
            X = 1,
            Y = Pos.Bottom(instrCheck),
            Value = config.SafeMode ? CheckState.Checked : CheckState.UnChecked,
        };
        var coverArtCheck = new CheckBox
        {
            Text = "Use Spotify cover _Art",
            X = 1,
            Y = Pos.Bottom(safeCheck),
            Value = config.UseSpotifyCoverArt ? CheckState.Checked : CheckState.UnChecked,
        };

        var saveBtn = new Button
        {
            Text = "_Save",
            IsDefault = true,
            X = Pos.Align(Alignment.Center),
            Y = Pos.AnchorEnd(1),
        };
        var cancelBtn = new Button
        {
            Text = "_Cancel",
            X = Pos.Align(Alignment.Center),
            Y = Pos.AnchorEnd(1),
        };

        saveBtn.Accepting += (s, e) =>
        {
            e.Handled = true;
            config.Variants =
            [
                .. (variantsField.Text?.ToString() ?? "")
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x)),
            ];
            if (int.TryParse(minField.Text.ToString(), out int min))
                config.DurationMin = min;
            if (int.TryParse(maxField.Text.ToString(), out int max))
                config.DurationMax = max;
            config.GenerateM3u = m3uCheck.Value == CheckState.Checked;
            config.ExcludeInstrumentals = instrCheck.Value == CheckState.Checked;
            config.SafeMode = safeCheck.Value == CheckState.Checked;
            config.UseSpotifyCoverArt = coverArtCheck.Value == CheckState.Checked;
            App!.RequestStop();
        };

        cancelBtn.Accepting += (s, e) =>
        {
            e.Handled = true;
            App!.RequestStop();
        };

        Add(
            variantsLabel,
            variantsField,
            minLabel,
            minField,
            maxLabel,
            maxField,
            m3uCheck,
            instrCheck,
            safeCheck,
            coverArtCheck,
            saveBtn,
            cancelBtn
        );
    }
}
