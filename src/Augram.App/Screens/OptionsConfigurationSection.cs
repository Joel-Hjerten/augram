using Augram.App.Declarations;
using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Screens;

/// <summary>
/// The Export and import section of Options › Sync, after Sync (plan 0003, Question 4 as proposed; "Configuration" until
/// plan 0006 gave Options sub-tabs): the Augram file's Export… and Import…, the StrokesPlus.net import beside them, and the
/// last outcome line once there is one.
/// </summary>
public static class OptionsConfigurationSection
{
    public const string Title = "Export and import";
    public const string AugramFileLabel = "Augram file";
    public const string StrokesPlusLabel = "StrokesPlus.net";
    public const string ExportLabel = "Export…";
    public const string ImportLabel = "Import…";
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
            new CustomField(AugramFileLabel, () => Buttons((ExportLabel, () => _ = vm.ExportAsync()), (ImportLabel, () => _ = vm.ImportAsync())), vm, AugramFileHelp),
            new CustomField(StrokesPlusLabel, () => Buttons((StrokesPlusImportLabel, () => _ = vm.ImportStrokesPlusAsync())), vm, StrokesPlusHelp),
            new NoteField("Last result", new DelegateBinding<string>(() => vm.Status, owner: vm))
            {
                Visible = new DelegateBinding<bool>(() => vm.HasStatus, owner: vm, propertyName: nameof(vm.HasStatus)),
            },
        ], SectionHelp);
    }

    /// <summary>Toolbar buttons side by side on one form line.</summary>
    private static StackPanel Buttons(params (string Label, Action Click)[] buttons)
    {
        var line = new StackPanel();
        line.Classes.Add("field-buttons");
        foreach (var (label, click) in buttons)
        {
            var button = new Button { Content = label };
            button.Classes.Add("toolbar");
            button.Click += (_, _) => click();
            line.Children.Add(button);
        }

        return line;
    }
}
