namespace Augram.App.Components.CommandTree;

/// <summary>
/// The one per-platform keymap of the Commands tab (F5a), shared by the tree and the step list: rename
/// is F2 on Windows and Return on macOS (Finder convention); the Ctrl shortcuts become Cmd there.
/// Avalonia key-gesture syntax. <see cref="Current"/> is this machine's; tests build the other one.
/// </summary>
public sealed class CommandsKeymap
{
    private readonly string _modifier;

    public CommandsKeymap(bool isMacOS)
    {
        IsMacOS = isMacOS;
        _modifier = isMacOS ? "Cmd" : "Ctrl";
    }

    public static CommandsKeymap Current { get; } = new(OperatingSystem.IsMacOS());

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

    public string Undo => _modifier + "+Z";

    public string Redo => IsMacOS ? "Cmd+Shift+Z" : "Ctrl+Y";

    /// <summary>Every gesture, for the test that proves they all parse.</summary>
    public IReadOnlyList<string> All => [New, Copy, Paste, Duplicate, Delete, Rename, Undo, Redo];
}
