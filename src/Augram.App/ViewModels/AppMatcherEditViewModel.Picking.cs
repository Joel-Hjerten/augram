using System.ComponentModel;
using Augram.App.Components.WindowFinder;
using Augram.App.Hosting;
using Augram.Core.Abstractions;

namespace Augram.App.ViewModels;

/// <summary>
/// The window finder's half of the identification form (F5 app identification; SP.net's crosshair, Joel 2026-10-09): what a
/// magnifier's pick puts into which field. "Identify window" adds the window's executable to this platform's list and shows
/// the rest (<see cref="Identified"/>) with a Use button each; the magnifier beside a field fills that field alone. Every fill
/// is a property change like typing, so the host applies it and its undo takes it back; a fill that sets two properties (the
/// path or title and its regex toggle) is one change (<see cref="Edit"/>), so one undo step. A window without the value (a
/// protected process's path, an untitled window, a Mac window's per-window fields) fills nothing.
/// </summary>
public sealed partial class AppMatcherEditViewModel
{
    private int _editDepth;
    private bool _editChanged;

    /// <summary>The platform Augram runs on: a picked window is one of its windows, so its executable goes into this platform's list.</summary>
    public HostPlatform Platform { get; init; } = CommandsModule.CurrentPlatform;

    /// <summary>The window "Identify window" last picked, with its other properties for the Use buttons. Not a matcher field.</summary>
    public IdentifiedWindowViewModel Identified { get; } = new();

    /// <summary>"Identify window": the window's executable joins this platform's names, and its other properties are offered.</summary>
    public void IdentifyWindow(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var list = $"{PlatformName(Platform)} executables";
        var outcome = TakeExecutable(window)
            ? $"added {window.ProcessName} to the {list}"
            : $"{window.ProcessName} is already in the {list}";
        Identified.Show(window, $"{WindowFinder.Summary(window)}: {outcome}.");
    }

    /// <summary>Adds the executable to this platform's names unless it is listed already (compared as matching does: trimmed, any case); true when added.</summary>
    public bool TakeExecutable(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var names = Split(Platform == HostPlatform.MacOS ? MacNames : WindowsNames);
        if (names.Contains(window.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var joined = string.Join(", ", names.Append(window.ProcessName));
        if (Platform == HostPlatform.MacOS)
        {
            MacNames = joined;
        }
        else
        {
            WindowsNames = joined;
        }

        return true;
    }

    /// <summary>The executable's full path, into this platform's path field, matched exactly (the regex toggle off).</summary>
    public void TakePath(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.ProcessPath is not { Length: > 0 } path)
        {
            return;
        }

        Edit(() =>
        {
            if (Platform == HostPlatform.MacOS)
            {
                MacPathIsRegex = false;
                MacProcessPath = path;
            }
            else
            {
                PathIsRegex = false;
                ProcessPath = path;
            }
        });
    }

    /// <summary>The window's title, matched exactly (the regex toggle off).</summary>
    public void TakeTitle(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.Title is { Length: > 0 } title)
        {
            Edit(() =>
            {
                TitleIsRegex = false;
                WindowTitle = title;
            });
        }
    }

    public void UseIdentifiedPath() => UseIdentified(TakePath);

    public void UseIdentifiedTitle() => UseIdentified(TakeTitle);

    /// <summary>While <see cref="Edit"/> runs, changes are held back and raised once afterwards.</summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_editDepth > 0)
        {
            _editChanged = true;
            return;
        }

        base.OnPropertyChanged(e);
    }

    /// <summary>What a field's magnifier shows while dragging: the value it would take, or why there is none.</summary>
    private static string PathOf(WindowIdentity window) => window.ProcessPath ?? $"{window.ProcessName}: path not readable";

    private static string TitleOf(WindowIdentity window) => string.IsNullOrEmpty(window.Title) ? $"{window.ProcessName}: no title" : window.Title;

    /// <summary>The magnifier beside <paramref name="platform"/>'s executables: only this platform's, since only its windows are on screen.</summary>
    private Func<Avalonia.Controls.Control>? ExecutableFinder(HostPlatform platform)
        => platform == Platform ? WindowFinderAccessory.Finder(window => window.ProcessName, window => TakeExecutable(window)) : null;

    private void UseIdentified(Action<WindowIdentity> take)
    {
        if (Identified.Window is { } window)
        {
            take(window);
        }
    }

    /// <summary>
    /// Several properties set as one edit: one change notification for every property (an empty name) afterwards, so the host
    /// applies once (one undo step) and every bound field re-reads its value.
    /// </summary>
    private void Edit(Action change)
    {
        _editDepth++;
        try
        {
            change();
        }
        finally
        {
            _editDepth--;
        }

        if (_editDepth == 0 && _editChanged)
        {
            _editChanged = false;
            base.OnPropertyChanged(new PropertyChangedEventArgs(string.Empty));
        }
    }
}
