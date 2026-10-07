using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The app group form's edit state (F5 app identification, F5a "edit app definition"): name, the
/// executable names as comma-separated text, window title with its regex toggle, suppress globals,
/// active. <see cref="Declare"/> is the form as a <see cref="FormScreen"/> (ADR-0002 §5c), shown in a
/// <c>FormDialog</c> for a new group and in the Apps tab's side panel for the selected one;
/// <see cref="ToGroup"/> and <see cref="Apply"/> turn the text back into an <see cref="AppMatcher"/>, keeping
/// the matcher fields this form does not edit (path, class chain, full-screen rule); <see cref="SyncFrom"/>
/// re-reads a stored group after an undo or a rename in the tree. Nothing is validated here: the store's
/// rules answer when the dialog confirms or the panel applies.
/// </summary>
public sealed partial class GroupEditViewModel : ObservableObject
{
    private AppMatcher _matcher;

    public GroupEditViewModel()
        : this(AppMatcher.Empty)
    {
    }

    private GroupEditViewModel(AppMatcher matcher)
    {
        _matcher = matcher;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Executable file names, comma-separated: "chrome.exe, msedge.exe".</summary>
    [ObservableProperty]
    public partial string ProcessNames { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TitleIsRegex { get; set; }

    [ObservableProperty]
    public partial bool SuppressGlobals { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    public static GroupEditViewModel From(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var matcher = group.Matcher ?? AppMatcher.Empty;
        return new GroupEditViewModel(matcher)
        {
            Name = group.Name,
            ProcessNames = string.Join(", ", matcher.ProcessNames),
            WindowTitle = matcher.Title ?? string.Empty,
            TitleIsRegex = matcher.TitleIsRegex,
            SuppressGlobals = group.SuppressGlobals,
            IsActive = group.IsActive,
        };
    }

    /// <summary>Takes the stored group's values; a property that already holds the value raises nothing.</summary>
    public void SyncFrom(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _matcher = group.Matcher ?? AppMatcher.Empty;
        Name = group.Name;
        if (!_matcher.ProcessNames.SequenceEqual(ProcessNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal))
        {
            ProcessNames = string.Join(", ", _matcher.ProcessNames);
        }

        WindowTitle = _matcher.Title ?? string.Empty;
        TitleIsRegex = _matcher.TitleIsRegex;
        SuppressGlobals = group.SuppressGlobals;
        IsActive = group.IsActive;
    }

    /// <summary>A new group with these settings and no commands.</summary>
    public AppGroup ToGroup(GroupId id) => new(id, Name, IsActive, SuppressGlobals, Matcher(), []);

    /// <summary>The existing group with these settings, its commands untouched.</summary>
    public AppGroup Apply(AppGroup existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with { Name = Name, IsActive = IsActive, SuppressGlobals = SuppressGlobals, Matcher = Matcher() };
    }

    /// <summary>The examples follow the platform: the executable's file name is "chrome.exe" on Windows and "Google Chrome" on macOS (Diagnostics and the log show it for each gesture).</summary>
    public static string ProcessNamesHelp => OperatingSystem.IsMacOS()
        ? "Comma-separated; any of them matches. The app's executable name, for example: Google Chrome, Safari"
        : "Comma-separated; any of them matches: chrome.exe, msedge.exe";

    public FormScreen Declare() => new("App group",
    [
        new Section("App group",
        [
            new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown in the command tree; unique among groups."),
            new ToggleField("Active", new DelegateBinding<bool>(() => IsActive, value => IsActive = value, this), "An inactive group and its commands never fire."),
            new ToggleField("Suppress global commands", new DelegateBinding<bool>(() => SuppressGlobals, value => SuppressGlobals = value, this), "SP.net's \"No Global Actions\": over this app only its own commands fire."),
        ]),
        new Section("App identification",
        [
            new TextField("Executable names", new DelegateBinding<string>(() => ProcessNames, value => ProcessNames = value, this), ProcessNamesHelp),
            new TextField("Window title", new DelegateBinding<string>(() => WindowTitle, value => WindowTitle = value, this), "Exact, case-insensitive, unless the toggle below makes it a pattern."),
            new ToggleField("Title is a regular expression", new DelegateBinding<bool>(() => TitleIsRegex, value => TitleIsRegex = value, this)),
            new NoteField("Pick a window", "A crosshair that fills these fields from a window on screen comes in a later slice; type the executable name for now."),
        ], "Every filled field must match; empty fields are ignored. A group with nothing filled in matches nothing."),
    ]);

    private AppMatcher Matcher() => _matcher with
    {
        ProcessNames = ProcessNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Title = string.IsNullOrWhiteSpace(WindowTitle) ? null : WindowTitle.Trim(),
        TitleIsRegex = TitleIsRegex,
    };
}
