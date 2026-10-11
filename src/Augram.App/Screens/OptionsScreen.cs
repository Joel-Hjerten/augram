using System.Runtime.CompilerServices;
using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Recognition;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Screens;

/// <summary>
/// The Options tab (F6, F7, A10, F8 sync) as five sub-tabs (plan 0006 decision 9), each a section/field tree: General,
/// Strokes (Capture, Recognition), Appearance (Trail), Sync (Sync, Export and import) and About. <c>AppNavigation</c> lists
/// them under one entry, as it does Diagnostics'. Moving a field is moving a line here; moving a section to another
/// sub-tab is moving it from one <c>Declare…</c> list to another.
/// </summary>
public static class OptionsScreen
{
    public const string GeneralTitle = "General";
    public const string StrokesTitle = "Strokes";
    public const string AppearanceTitle = "Appearance";
    public const string SyncTitle = "Sync";

    public const string StartAtLoginHelp = "Also in the tray menu.";
    public const string StartAtLoginDevHelp = "Start at login applies to the installed Augram; this is a development build.";
    public const string StartAtLoginNoteLabel = "At login";
    public const string StartAtLoginNoteHelp =
        "Start at login was changed outside Augram, or the system has not accepted it yet. When Augram starts it follows a switch-off made there; ticking Start at login turns it on again.";
    public const string AboutTitle = "About";
    public const string IgnoreKeysHelp = "Hold any ticked key when you press the stroke button to use the button normally, with no gesture.";
    public const string MenuBarIconHelp = "The app icon in colour instead of the single-colour shape macOS tints for light and dark menu bars.";

    /// <summary>Options › General: the stroke button, ignore keys, start at login, the macOS menu-bar icon, the config folder.</summary>
    public static ScreenDeclaration DeclareGeneral(AppSettingsViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return SubTab(GeneralTitle, [GeneralSection(vm)]);
    }

    /// <summary>Options › Strokes: Capture, then Recognition.</summary>
    public static ScreenDeclaration DeclareStrokes(AppSettingsViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return SubTab(StrokesTitle, [CaptureSection(vm), RecognitionSection(vm)]);
    }

    /// <summary>Options › Appearance: Trail. Plan 0006's Theme section goes in front of it, as one more entry in this list.</summary>
    public static ScreenDeclaration DeclareAppearance(AppSettingsViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return SubTab(AppearanceTitle, [TrailSection(vm)]);
    }

    /// <summary>
    /// Options › Sync: Sync, then Export and import. Each is left out while its view model is null (tests, a root without
    /// <c>SyncModule</c> or <c>TransferModule</c>), so the sub-tab shows what it has.
    /// </summary>
    /// <param name="sync">The Sync section's (<see cref="OptionsSyncSection"/>).</param>
    /// <param name="configuration">The Export and import section's (<see cref="OptionsConfigurationSection"/>).</param>
    public static ScreenDeclaration DeclareSync(SyncViewModel? sync, ConfigurationViewModel? configuration)
    {
        var sections = new List<Section>();
        if (sync is not null)
        {
            sections.Add(OptionsSyncSection.Declare(sync));
        }

        if (configuration is not null)
        {
            sections.Add(OptionsConfigurationSection.Declare(configuration));
        }

        return SubTab(SyncTitle, sections);
    }

    /// <summary>Options › About: which build this is.</summary>
    public static ScreenDeclaration DeclareAbout(AppSettingsViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return SubTab(AboutTitle, [AboutSection(vm)]);
    }

    /// <summary>Titled with the sub-tab's path, so the F1 inspector names a field where the user finds it: "Options › Strokes › Capture › Start distance (px)".</summary>
    private static FormScreen SubTab(string title, IReadOnlyList<Section> sections, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        new($"Options › {title}", sections, file, line);

    /// <summary>Which build this is, read-only: version, commit and channel (<see cref="Hosting.AppInfo"/>).</summary>
    private static Section AboutSection(AppSettingsViewModel vm) => new(AboutTitle,
    [
        new NoteField("Version", vm.App.Version),
        new NoteField("Commit", vm.App.Commit ?? "unknown"),
        new NoteField("Channel", vm.App.ChannelText),
    ]);

    /// <summary>Two-way in the installed build; read-only (the renderer disables it) with the reason in a development build.</summary>
    private static ToggleField StartAtLogin(AppSettingsViewModel vm) => vm.CanChangeStartAtLogin
        ? new ToggleField("Start at login", new DelegateBinding<bool>(() => vm.StartAtLogin, v => vm.StartAtLogin = v, vm), StartAtLoginHelp)
        : new ToggleField("Start at login", new DelegateBinding<bool>(() => vm.StartAtLogin, owner: vm), StartAtLoginDevHelp);

    /// <summary>
    /// A state note under the toggle, shown only while there is one (Turned off in Task Manager › Startup apps; Waiting for
    /// approval in System Settings › General › Login Items): plain text, since it must be seen without hovering (Help rule).
    /// </summary>
    private static NoteField StartAtLoginNote(AppSettingsViewModel vm) =>
        new(StartAtLoginNoteLabel, new DelegateBinding<string>(() => vm.StartAtLoginNote, owner: vm), StartAtLoginNoteHelp)
        {
            Visible = new DelegateBinding<bool>(() => vm.HasStartAtLoginNote, owner: vm),
        };

    /// <summary>macOS only (<see cref="Hosting.AppState.CanChooseMenuBarIcon"/>): the colour icon or the tinted template, as in Eyeris.</summary>
    private static Field[] MenuBarIcon(AppSettingsViewModel vm) => Hosting.AppState.CanChooseMenuBarIcon
        ? [new ToggleField("Colour menu-bar icon", new DelegateBinding<bool>(() => vm.ColourMenuBarIcon, v => vm.ColourMenuBarIcon = v, vm), MenuBarIconHelp)]
        : [];

    /// <summary>
    /// The ignore keys as one row of check boxes (Joel, 2026-10-09: several may be ticked, as in StrokesPlus.net). Holding
    /// any ticked key when the stroke button goes down passes the button through. Captions are this platform's key names,
    /// as hotkeys show them (Win on Windows; Opt and Cmd on a Mac); the stored set is the same everywhere.
    /// </summary>
    private static TogglesField IgnoreKeyToggles(AppSettingsViewModel vm)
    {
        ToggleOption Key(IgnoreKeys key) => new(
            HotkeyText.Format((KeyModifiers)(int)key, KeyCode.None),
            new DelegateBinding<bool>(() => vm.IgnoreKey.HasFlag(key), on => vm.IgnoreKey = on ? vm.IgnoreKey | key : vm.IgnoreKey & ~key, vm));

        return new TogglesField(
            "Ignore keys",
            [Key(IgnoreKeys.Control), Key(IgnoreKeys.Alt), Key(IgnoreKeys.Shift), Key(IgnoreKeys.Win)],
            IgnoreKeysHelp);
    }

    private static Section GeneralSection(AppSettingsViewModel vm) => new("General",
    [
        new ButtonRadioField<MouseButton>("Stroke button", Choice.FromEnum<MouseButton>(),
            new DelegateBinding<MouseButton>(() => vm.StrokeButton, v => vm.StrokeButton = v, vm),
            "Hold this button and draw. Right is the fresh-install default; SP.net keeps Middle while it runs."),
        new CustomField("Detect button", () => DetectButtonEditor(vm), vm,
            "Press the button you want within 5 seconds. If nothing is seen, its vendor software consumes it before Augram can."),
        IgnoreKeyToggles(vm),
        StartAtLogin(vm),
        StartAtLoginNote(vm),
        .. MenuBarIcon(vm),
        new TextField("Config folder",
            new DelegateBinding<string>(() => vm.ConfigFolder, owner: vm),
            "Settings, gestures and logs live here; change it via the --config-folder <path> argument."),
    ]);

    private static Section CaptureSection(AppSettingsViewModel vm) => new("Capture",
    [
        new NumberField("Start distance (px)",
            new DelegateBinding<double>(() => vm.StartDistancePx, v => vm.StartDistancePx = v, vm), 1, 200,
            Help: "Movement before a press counts as a stroke rather than a click."),
        new NumberField("Button drag distance (px)",
            new DelegateBinding<double>(() => vm.ButtonDragDistancePx, v => vm.ButtonDragDistancePx = v, vm), 1, 200,
            Help: "For a button other than the stroke button that a trigger holds (Right in Right + wheel): movement before its press goes to the app as a drag. Lower starts drags sooner; too low and a wobble while turning the wheel gives the press away."),
        new NumberField("Cancel delay (ms)",
            new DelegateBinding<double>(() => vm.CancelDelayMs, v => vm.CancelDelayMs = v, vm), 0, 5000, 50,
            "Holding still this long cancels the stroke and replays the click."),
        new DropdownField<NoMatchBehaviour>("When nothing matches", Choice.FromEnum<NoMatchBehaviour>(),
            new DelegateBinding<NoMatchBehaviour>(() => vm.NoMatch, v => vm.NoMatch = v, vm)),
    ]);

    /// <summary>The trail's colour is picked from the rainbow swatches or with Custom… (plan 0006 decision 8).</summary>
    private static Section TrailSection(AppSettingsViewModel vm) => new("Trail",
    [
        new ColorField("Colour", new DelegateBinding<RgbColor>(() => vm.TrailColour, v => vm.TrailColour = v, vm))
        {
            Presets = ColourPresets.Rainbow,
        },
        new NumberField("Width (px)", new DelegateBinding<double>(() => vm.TrailWidth, v => vm.TrailWidth = v, vm), 1, 20),
        new NumberField("Opacity", new DelegateBinding<double>(() => vm.TrailOpacity, v => vm.TrailOpacity = v, vm), 0, 1, 0.05),
    ], "Scales with the DPI of the monitor the stroke starts on.");

    private static Section RecognitionSection(AppSettingsViewModel vm) => new("Recognition",
    [
        new NumberField("Threshold", new DelegateBinding<double>(() => vm.Threshold, v => vm.Threshold = v, vm), 0, 100,
            Help: "Minimum score (0–100) for a match to fire."),
        new NumberField("Precision", new DelegateBinding<double>(() => vm.Precision, v => vm.Precision = v, vm), 10, 400, 10,
            "Resample count; templates stay raw so this can change any time."),
        new DropdownField<ScoringMode>("Scoring mode", Choice.FromEnum<ScoringMode>(),
            new DelegateBinding<ScoringMode>(() => vm.ScoringMode, v => vm.ScoringMode = v, vm),
            "Corrected scores as StrokesPlus.net does. Legacy is the classic StrokesPlus maths, slightly more forgiving, kept for comparison."),
    ]);

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
