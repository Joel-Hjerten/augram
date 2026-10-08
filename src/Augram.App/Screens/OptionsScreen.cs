using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Recognition;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Screens;

/// <summary>The Options tab (F6, F7, A10, F8 sync) as a section/field tree. Moving a field is moving a line here.</summary>
public static class OptionsScreen
{
    public const string StartAtLoginHelp = "Also in the tray menu.";
    public const string StartAtLoginDevHelp = "Start at login applies to the installed Augram; this is a development build.";
    public const string AboutTitle = "About";

    /// <param name="vm">The settings projection.</param>
    /// <param name="sync">Options › Sync (<see cref="OptionsSyncSection"/>); null leaves the section out (tests, a root without <c>SyncModule</c>).</param>
    public static ScreenDeclaration Declare(AppSettingsViewModel vm, SyncViewModel? sync = null)
    {
        ArgumentNullException.ThrowIfNull(vm);
        var sections = Sections(vm);
        return new FormScreen("Options", sync is null ? [.. sections, About(vm)] : [.. sections, OptionsSyncSection.Declare(sync), About(vm)]);
    }

    /// <summary>Which build this is, read-only: version, commit and channel (<see cref="Hosting.AppInfo"/>).</summary>
    private static Section About(AppSettingsViewModel vm) => new(AboutTitle,
    [
        new NoteField("Version", vm.App.Version),
        new NoteField("Commit", vm.App.Commit ?? "unknown"),
        new NoteField("Channel", vm.App.ChannelText),
    ]);

    /// <summary>Two-way in the installed build; read-only (the renderer disables it) with the reason in a development build.</summary>
    private static ToggleField StartAtLogin(AppSettingsViewModel vm) => vm.CanChangeStartAtLogin
        ? new ToggleField("Start at login", new DelegateBinding<bool>(() => vm.StartAtLogin, v => vm.StartAtLogin = v, vm), StartAtLoginHelp)
        : new ToggleField("Start at login", new DelegateBinding<bool>(() => vm.StartAtLogin, owner: vm), StartAtLoginDevHelp);

    private static IReadOnlyList<Section> Sections(AppSettingsViewModel vm)
    {
        return
        [
            new Section("General",
            [
                new ButtonRadioField<MouseButton>("Stroke button", Choice.FromEnum<MouseButton>(),
                    new DelegateBinding<MouseButton>(() => vm.StrokeButton, v => vm.StrokeButton = v, vm),
                    "Hold this button and draw. Right is the fresh-install default; SP.net keeps Middle while it runs."),
                new CustomField("Detect button", () => DetectButtonEditor(vm), vm,
                    "Press the button you want within 5 seconds. If nothing is seen, its vendor software consumes it before Augram can."),
                new DropdownField<IgnoreKeys>("Ignore key", Choice.FromEnum<IgnoreKeys>(),
                    new DelegateBinding<IgnoreKeys>(() => vm.IgnoreKey, v => vm.IgnoreKey = v, vm),
                    "Hold this key to use the stroke button normally."),
                StartAtLogin(vm),
                new TextField("Config folder",
                    new DelegateBinding<string>(() => vm.ConfigFolder, owner: vm),
                    "Settings, gestures and logs live here; change it via the --config-folder <path> argument."),
            ]),
            new Section("Capture",
            [
                new NumberField("Start distance (px)",
                    new DelegateBinding<double>(() => vm.StartDistancePx, v => vm.StartDistancePx = v, vm), 1, 200,
                    Help: "Movement before a press counts as a stroke rather than a click."),
                new NumberField("Cancel delay (ms)",
                    new DelegateBinding<double>(() => vm.CancelDelayMs, v => vm.CancelDelayMs = v, vm), 0, 5000, 50,
                    "Holding still this long cancels the stroke and replays the click."),
                new DropdownField<NoMatchBehaviour>("When nothing matches", Choice.FromEnum<NoMatchBehaviour>(),
                    new DelegateBinding<NoMatchBehaviour>(() => vm.NoMatch, v => vm.NoMatch = v, vm)),
            ]),
            new Section("Trail",
            [
                new ColorField("Colour", new DelegateBinding<RgbColor>(() => vm.TrailColour, v => vm.TrailColour = v, vm)),
                new NumberField("Width (px)", new DelegateBinding<double>(() => vm.TrailWidth, v => vm.TrailWidth = v, vm), 1, 20),
                new NumberField("Opacity", new DelegateBinding<double>(() => vm.TrailOpacity, v => vm.TrailOpacity = v, vm), 0, 1, 0.05),
            ], "Scales with the DPI of the monitor the stroke starts on."),
            new Section("Recognition",
            [
                new NumberField("Threshold", new DelegateBinding<double>(() => vm.Threshold, v => vm.Threshold = v, vm), 0, 100,
                    Help: "Minimum score (0–100) for a match to fire."),
                new NumberField("Precision", new DelegateBinding<double>(() => vm.Precision, v => vm.Precision = v, vm), 10, 400, 10,
                    "Resample count; templates stay raw so this can change any time."),
                new DropdownField<ScoringMode>("Scoring mode", Choice.FromEnum<ScoringMode>(),
                    new DelegateBinding<ScoringMode>(() => vm.ScoringMode, v => vm.ScoringMode = v, vm),
                    "Legacy reproduces StrokesPlus exactly, quirks included."),
            ]),
        ];
    }

    /// <summary>A button that starts detect-to-assign (F1) and a status line beside it.</summary>
    private static Control DetectButtonEditor(AppSettingsViewModel vm)
    {
        var detect = ToolbarButton("Detect…", vm.DetectButton, new DelegateBinding<bool>(() => !vm.IsDetecting, owner: vm, propertyName: nameof(vm.IsDetecting)));
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        status.Classes.Add("help");
        BindingObserver.Attach(status, new DelegateBinding<string>(() => vm.DetectStatus, owner: vm), value => status.Text = value);
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { detect, status } };
    }

    private static Button ToolbarButton(string label, Action click, IValueBinding<bool> enabled)
    {
        var button = new Button { Content = label };
        button.Classes.Add("toolbar");
        button.Click += (_, _) => click();
        BindingObserver.Attach(button, enabled, value => button.IsEnabled = value);
        return button;
    }
}
