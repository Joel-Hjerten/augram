using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The StrokesPlus.net app definition's per-window fields (Joel, 2026-10-09: every field, each with Use Regex; some may go
/// later): the root's, the parent's and the control's title, and the owner's, root's, parent's and control's class, in a
/// section of their own because they are Windows-only and rarely needed. Each has a regex toggle and, on Windows, a
/// magnifier that fills it from the window under it (toggle off). <see cref="WindowField"/> is the one list the section,
/// the copy to and from the matcher, the magnifiers and the identified window's Use rows walk.
/// </summary>
public sealed partial class AppMatcherEditViewModel
{
    public const string WindowDetailsTitle = "Window details (Windows)";

    [ObservableProperty]
    public partial string RootTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RootTitleIsRegex { get; set; }

    [ObservableProperty]
    public partial string ParentTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ParentTitleIsRegex { get; set; }

    [ObservableProperty]
    public partial string ControlTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ControlTitleIsRegex { get; set; }

    [ObservableProperty]
    public partial string OwnerClass { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool OwnerClassIsRegex { get; set; }

    [ObservableProperty]
    public partial string RootClass { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RootClassIsRegex { get; set; }

    [ObservableProperty]
    public partial string ParentClass { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ParentClassIsRegex { get; set; }

    [ObservableProperty]
    public partial string ControlClass { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ControlClassIsRegex { get; set; }

    /// <summary>
    /// One per-window field: its label ("Root title"), the window's value it reads (<see cref="Of"/>), the form's text and
    /// toggle, and the matcher's.
    /// </summary>
    internal sealed record WindowField(
        string Label,
        string Help,
        Func<WindowLevels, string?> Of,
        Func<string> Text,
        Action<string> SetText,
        Func<bool> IsRegex,
        Action<bool> SetIsRegex,
        Func<AppMatcher, (string? Value, bool IsRegex)> FromMatcher);

    internal IReadOnlyList<WindowField> WindowFields =>
    [
        new("Root title", "The top window of the parent chain (a dialog, not the app's main window behind it).", levels => levels.RootTitle,
            () => RootTitle, value => RootTitle = value, () => RootTitleIsRegex, value => RootTitleIsRegex = value, matcher => (matcher.RootTitle, matcher.RootTitleIsRegex)),
        new("Parent title", "The window that contains the control under the pointer.", levels => levels.ParentTitle,
            () => ParentTitle, value => ParentTitle = value, () => ParentTitleIsRegex, value => ParentTitleIsRegex = value, matcher => (matcher.ParentTitle, matcher.ParentTitleIsRegex)),
        new("Control title", "The text of the control under the pointer itself.", levels => levels.ControlTitle,
            () => ControlTitle, value => ControlTitle = value, () => ControlTitleIsRegex, value => ControlTitleIsRegex = value, matcher => (matcher.ControlTitle, matcher.ControlTitleIsRegex)),
        new("Owner class", "The class of the main window (the window activation brings forward).", levels => levels.OwnerClass,
            () => OwnerClass, value => OwnerClass = value, () => OwnerClassIsRegex, value => OwnerClassIsRegex = value, matcher => (matcher.OwnerClass, matcher.OwnerClassIsRegex)),
        new("Root class", "The class of the top window of the parent chain.", levels => levels.RootClass,
            () => RootClass, value => RootClass = value, () => RootClassIsRegex, value => RootClassIsRegex = value, matcher => (matcher.RootClass, matcher.RootClassIsRegex)),
        new("Parent class", "The class of the window that contains the control.", levels => levels.ParentClass,
            () => ParentClass, value => ParentClass = value, () => ParentClassIsRegex, value => ParentClassIsRegex = value, matcher => (matcher.ParentClass, matcher.ParentClassIsRegex)),
        new("Control class", "The class of the control under the pointer.", levels => levels.ControlClass,
            () => ControlClass, value => ControlClass = value, () => ControlClassIsRegex, value => ControlClassIsRegex = value, matcher => (matcher.ControlClass, matcher.ControlClassIsRegex)),
    ];

    /// <summary>The window's value for <paramref name="field"/>, matched exactly (its toggle off); a window without one fills nothing.</summary>
    internal void TakeWindowField(WindowField field, WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(window);
        if (field.Of(window.Levels) is { Length: > 0 } value)
        {
            Edit(() =>
            {
                field.SetIsRegex(false);
                field.SetText(value);
            });
        }
    }

    private void SyncWindowFieldsFrom(AppMatcher matcher)
    {
        foreach (var field in WindowFields)
        {
            var (value, isRegex) = field.FromMatcher(matcher);
            field.SetText(value ?? string.Empty);
            field.SetIsRegex(isRegex);
        }
    }

    private AppMatcher WithWindowFields(AppMatcher matcher) => matcher with
    {
        RootTitle = Trimmed(RootTitle),
        RootTitleIsRegex = RootTitleIsRegex,
        ParentTitle = Trimmed(ParentTitle),
        ParentTitleIsRegex = ParentTitleIsRegex,
        ControlTitle = Trimmed(ControlTitle),
        ControlTitleIsRegex = ControlTitleIsRegex,
        OwnerClass = Trimmed(OwnerClass),
        OwnerClassIsRegex = OwnerClassIsRegex,
        RootClass = Trimmed(RootClass),
        RootClassIsRegex = RootClassIsRegex,
        ParentClass = Trimmed(ParentClass),
        ParentClassIsRegex = ParentClassIsRegex,
        ControlClass = Trimmed(ControlClass),
        ControlClassIsRegex = ControlClassIsRegex,
    };

    /// <summary>
    /// Each per-window field with its toggle (and a magnifier on Windows), then the older "Window classes" list, shown only
    /// while it still holds classes (configs from before 2026-10-09: the Windows Desktop, VMware).
    /// </summary>
    private Section WindowDetailsSection()
    {
        var fields = new List<Field>();
        foreach (var field in WindowFields)
        {
            fields.Add(new TextField(field.Label, new DelegateBinding<string>(field.Text, field.SetText, this), field.Help + " Exact, case-insensitive, unless the toggle below makes it a pattern.")
            {
                Accessory = Platform == HostPlatform.Windows ? WindowFinderAccessory.Finder(window => DescribeLevel(field, window), window => TakeWindowField(field, window)) : null,
            });
            fields.Add(new ToggleField($"{field.Label} is a regular expression", new DelegateBinding<bool>(field.IsRegex, field.SetIsRegex, this)));
        }

        fields.Add(new TextField("Window classes (older)", new DelegateBinding<string>(() => WindowClasses, value => WindowClasses = value, this), "From before the fields above: each class must be somewhere among the window's classes; Progman|WorkerW lists alternatives. Move them to the class fields above, then empty this.")
        {
            Visible = new DelegateBinding<bool>(() => WindowClasses.Trim().Length > 0, owner: this),
        });
        return new Section(WindowDetailsTitle, fields, "StrokesPlus.net's app definition fields. Windows only: on a Mac a group that sets one matches nothing.");
    }

    /// <summary>The identified window's value for each per-window field, with Use; a row only while the window has that value.</summary>
    private IEnumerable<Field> IdentifiedWindowFieldRows() => WindowFields.Select(field => (Field)new NoteField(
        $"Its {char.ToLowerInvariant(field.Label[0])}{field.Label[1..]}",
        new DelegateBinding<string>(() => Identified.Window is { } window ? field.Of(window.Levels) ?? string.Empty : string.Empty, owner: Identified),
        $"Use makes it the {field.Label.ToLowerInvariant()} under {WindowDetailsTitle}.")
    {
        Visible = new DelegateBinding<bool>(() => Identified.Window is { } window && !string.IsNullOrEmpty(field.Of(window.Levels)), owner: Identified),
        Accessory = WindowFinderAccessory.UseButton(() => UseIdentified(window => TakeWindowField(field, window))),
    });

    private static string DescribeLevel(WindowField field, WindowIdentity window)
        => field.Of(window.Levels) is { Length: > 0 } value ? value : $"{window.ProcessName}: no {field.Label.ToLowerInvariant()}";
}
