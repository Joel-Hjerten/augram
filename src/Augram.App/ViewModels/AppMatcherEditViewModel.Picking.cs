using System.ComponentModel;
using Augram.App.Hosting;
using Augram.Core.Abstractions;

namespace Augram.App.ViewModels;

/// <summary>
/// The window finder's half of the identification form (F5 app identification; SP.net's crosshair, Joel 2026-10-09): the
/// magnifier on each row fills that row from the window it is dropped on, into this machine's platform's field (only its
/// windows are on screen). The executable joins the list; every other value replaces the field and switches its regex
/// toggle off. Every fill is a property change like typing, so the host applies it and its undo takes it back; a fill that
/// sets two properties (a value and its toggle) is one change (<see cref="Edit"/>), so one undo step. A window without the
/// value (a protected process's path, an untitled window, a Mac window's per-window fields) fills nothing.
/// </summary>
public sealed partial class AppMatcherEditViewModel
{
    private int _editDepth;
    private bool _editChanged;

    /// <summary>The platform Augram runs on: a picked window is one of its windows, so its values go into this platform's fields.</summary>
    public HostPlatform Platform { get; init; } = CommandsModule.CurrentPlatform;

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

    /// <summary>
    /// The name an entry made from a picked window starts with (plan 0004: the "Not in" dialog's Add app…): its executable
    /// without ".exe", "Spine" for Spine.exe, a Mac app's name as it is ("Google Chrome").
    /// </summary>
    public static string AppNameOf(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var name = window.ProcessName.Trim();
        return name.Length > 4 && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    /// <summary>The executable's full path, into this platform's path, matched exactly (the regex toggle off).</summary>
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

    /// <summary>The window's title, into this platform's title, matched exactly (the regex toggle off).</summary>
    public void TakeTitle(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.Title is not { Length: > 0 } title)
        {
            return;
        }

        Edit(() =>
        {
            if (Platform == HostPlatform.MacOS)
            {
                MacTitleIsRegex = false;
                MacWindowTitle = title;
            }
            else
            {
                TitleIsRegex = false;
                WindowTitle = title;
            }
        });
    }

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
