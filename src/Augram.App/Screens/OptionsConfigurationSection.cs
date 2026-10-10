using Augram.App.Declarations;
using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Screens;

/// <summary>
/// The Configuration section of the Options tab (plan 0003, Question 4 as proposed; before About): the Augram file's
/// Export… and Import…, the StrokesPlus.net import beside them, and the last outcome line once there is one.
/// </summary>
public static class OptionsConfigurationSection
{
    public const string Title = "Configuration";
    public const string AugramFileLabel = "Augram file";
    public const string StrokesPlusLabel = "StrokesPlus.net";
    public const string ExportLabel = "Export…";
    public const string StrokesPlusImportLabel = "Import from StrokesPlus.net…";

    public const string SectionHelp =
        "Move gestures, commands and options between Augram configurations as a file: another machine, a friend, a fresh start.";

    public const string AugramFileHelp =
        "Export writes everything, the gestures only, or chosen app groups to an .augram.json file. Import reads one (or augram.json, a backup) and shows what is new, the same or different before anything changes; it never deletes.";

    public const string StrokesPlusHelp = "Gestures, actions and apps from StrokesPlus.net.json. Also on the Gestures tab.";

    public static Section Declare(ConfigurationViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new Section(Title,
        [
            new CustomField(AugramFileLabel, () => Buttons((ExportLabel, () => _ = vm.ExportAsync())), vm, AugramFileHelp),
            new CustomField(StrokesPlusLabel, () => Buttons((StrokesPlusImportLabel, () => _ = vm.ImportStrokesPlusAsync())), vm, StrokesPlusHelp),
            new NoteField("Last result", new DelegateBinding<string>(() => vm.Status, owner: vm))
            {
                Visible = new DelegateBinding<bool>(() => vm.HasStatus, owner: vm, propertyName: nameof(vm.HasStatus)),
            },
        ], SectionHelp);
    }

    /// <summary>Toolbar buttons side by side on one form line.</summary>
    private static DockPanel Buttons(params (string Label, Action Click)[] buttons)
    {
        var line = new DockPanel { LastChildFill = false };
        line.Classes.Add("field-line");
        foreach (var (label, click) in buttons)
        {
            var button = new Button { Content = label };
            button.Classes.Add("toolbar");
            button.Click += (_, _) => click();
            DockPanel.SetDock(button, Dock.Left);
            line.Children.Add(button);
        }

        return line;
    }
}
