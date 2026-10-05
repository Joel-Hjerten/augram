namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// Which windows count as "the desktop" for A20 and A21. The desktop icon view is a <c>SysListView32</c> under
/// <c>SHELLDLL_DefView</c> under <c>Progman</c> (or <c>WorkerW</c> when a wallpaper slideshow or a full-screen
/// app has re-parented it), so the root class is what identifies it (reference strokesplus-net-config §9).
/// </summary>
internal static class DesktopRule
{
    private static readonly string[] DesktopRootClasses = ["Progman", "WorkerW"];

    public static bool IsDesktopClass(string className)
        => Array.IndexOf(DesktopRootClasses, className) >= 0;

    /// <summary>True when any class on the chain is a desktop root class.</summary>
    public static bool IsDesktop(IReadOnlyList<string> classChain)
    {
        foreach (var name in classChain)
        {
            if (IsDesktopClass(name))
            {
                return true;
            }
        }

        return false;
    }
}
