using Augram.Core.Abstractions;

namespace Augram.App.Components.ListEditing;

/// <summary>
/// The one per-platform keymap of every editable list (F5a): the command tree, the step list, the Exclusions lists and the
/// gesture grid bind their keys from it, and the view models' messages name its keys ("Ctrl+Z undoes it"). Rename is F2 (or
/// Enter) on Windows and Return on macOS (Finder convention; Q11, Joel 2026-10-11: each platform's convention, with Enter as
/// Windows' second rename key wherever the Mac renames with Return, the gesture grid included), and a double click renames
/// too; the Ctrl shortcuts become Cmd there. Avalonia key-gesture syntax. <see cref="Current"/> is this machine's; tests build
/// the other one. No list binds a key of its own.
/// </summary>
public sealed class EditKeymap
{
    private readonly string _modifier;

    public EditKeymap(bool isMacOS)
    {
        IsMacOS = isMacOS;
        _modifier = isMacOS ? "Cmd" : "Ctrl";
    }

    public static EditKeymap Current { get; } = new(RunningPlatform.Current == HostPlatform.MacOS);

    public bool IsMacOS { get; }

    /// <summary>A new item in the focused level: a command in the tree, a step in the step list.</summary>
    public string New => _modifier + "+N";

    public string Copy => _modifier + "+C";

    /// <summary>A copied command into the selected group; a copied step at the bottom of the list.</summary>
    public string Paste => _modifier + "+V";

    /// <summary>A copy of the selected step directly below it.</summary>
    public string Duplicate => _modifier + "+D";

    public string Delete => "Delete";

    public string Rename => IsMacOS ? "Return" : "F2";

    /// <summary>A second rename key in every list: Enter on Windows, beside F2 (Joel, 2026-10-07; the gesture grid too since Q11); macOS already renames on Return.</summary>
    public string? RenameAlso => IsMacOS ? null : "Enter";

    public string Undo => _modifier + "+Z";

    public string Redo => IsMacOS ? "Cmd+Shift+Z" : "Ctrl+Y";

    /// <summary>The focus into the list's search box (plan 0009 decision 6).</summary>
    public string Find => _modifier + "+F";

    /// <summary>Every gesture, for the test that proves they all parse.</summary>
    public IReadOnlyList<string> All => [New, Copy, Paste, Duplicate, Delete, Rename, .. RenameAlso is { } also ? new[] { also } : [], Undo, Redo, Find];
}
