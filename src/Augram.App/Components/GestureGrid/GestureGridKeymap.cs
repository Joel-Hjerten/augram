namespace Augram.App.Components.GestureGrid;

/// <summary>
/// The one per-platform keymap for list editing (F5a): rename is F2 on Windows and Return on macOS
/// (Finder convention); the Ctrl shortcuts become Cmd there. Avalonia key-gesture syntax.
/// </summary>
public static class GestureGridKeymap
{
    public static string New => Modifier + "+N";

    public static string Rename => OperatingSystem.IsMacOS() ? "Return" : "F2";

    public static string Delete => "Delete";

    public static string Undo => Modifier + "+Z";

    public static string Redo => OperatingSystem.IsMacOS() ? "Cmd+Shift+Z" : "Ctrl+Y";

    private static string Modifier => OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl";
}
