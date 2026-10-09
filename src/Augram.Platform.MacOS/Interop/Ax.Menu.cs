namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// An app's menu bar through Accessibility, for MaximizeOrRestore's use of macOS's own window tiling (Joel, 2026-10-09:
/// "the green button's Fill"): Window › Move &amp; Resize › Fill and Return to Previous Size. Found without menu titles,
/// which are localised: the Window menu is the one holding Minimize (⌘M), and the tiling items are matched by their
/// key equivalents (a character with Control and no Command, plus the fn bit: measured 2026-10-09, VS Code lists Fill as
/// "F/28" = Control 4 + no Command 8 + fn 16). Window › Zoom has no shortcut ("Zoom /8"): it is matched by its title, or
/// by its place right after the Minimize items, for macOS's own zoom (a title-bar double-click).
/// </summary>
internal static partial class Ax
{
    public const string MenuBarAttribute = "AXMenuBar";
    public const string ChildrenAttribute = "AXChildren";
    public const string EnabledAttribute = "AXEnabled";
    public const string MenuItemCmdCharAttribute = "AXMenuItemCmdChar";
    public const string MenuItemCmdModifiersAttribute = "AXMenuItemCmdModifiers";

    /// <summary>kAXMenuItemModifierControl.</summary>
    public const long ControlModifier = 1 << 2;

    /// <summary>kAXMenuItemModifierNoCommand: the shortcut has no ⌘.</summary>
    public const long NoCommandModifier = 1 << 3;

    /// <summary>The fn (Globe) key, as macOS 15+ reports it for the tiling shortcuts (fn-Control-F); ignored when matching.</summary>
    public const long FunctionModifier = 1 << 4;

    private const string ZoomTitle = "Zoom";

    private const int MaxMenuDepth = 4;

    /// <summary>The children arrays a menu search copied on this thread, released by <see cref="ReleaseMenuSearch"/>.</summary>
    private static readonly ThreadLocal<List<nint>> ChildArrays = new(() => []);

    /// <summary>
    /// The enabled item of <paramref name="app"/>'s Window menu (or one of its submenus) whose shortcut is
    /// <paramref name="key"/> with <paramref name="modifiers"/>. Owned; zero when there is none. <paramref name="seen"/>
    /// collects every item looked at ("Fill ⌃F" style), for the log when nothing matched.
    /// </summary>
    public static nint FindWindowMenuItem(nint app, string key, long modifiers, List<string> seen)
    {
        ArgumentNullException.ThrowIfNull(seen);
        if (Copy(app, Cf.Constant(MenuBarAttribute), out var bar) != MacNative.AXErrorSuccess || bar == 0)
        {
            return 0;
        }

        try
        {
            foreach (var barItem in Children(bar))
            {
                foreach (var menu in Children(barItem))
                {
                    if (!HoldsMinimize(menu))
                    {
                        continue;
                    }

                    var found = Find(menu, key, modifiers, seen, 0);
                    if (found != 0)
                    {
                        return found;
                    }
                }
            }

            return 0;
        }
        finally
        {
            Cf.Release(bar);
        }
    }

    /// <summary>
    /// The enabled Window › Zoom item of <paramref name="app"/>: titled "Zoom", else the first item without a shortcut after
    /// the Minimize items (AppKit's order: Minimize, Minimize All, Zoom). Owned; zero when there is none.
    /// </summary>
    public static nint FindZoomMenuItem(nint app)
    {
        if (Copy(app, Cf.Constant(MenuBarAttribute), out var bar) != MacNative.AXErrorSuccess || bar == 0)
        {
            return 0;
        }

        try
        {
            foreach (var menu in Children(bar).SelectMany(Children).Where(HoldsMinimize))
            {
                var items = Children(menu);
                var byTitle = items.FirstOrDefault(item => string.Equals(GetString(item, TitleAttribute), ZoomTitle, StringComparison.OrdinalIgnoreCase));
                var afterMinimize = items
                    .SkipWhile(item => !IsMinimize(item))
                    .SkipWhile(IsMinimize)
                    .FirstOrDefault(item => string.IsNullOrEmpty(GetString(item, MenuItemCmdCharAttribute)) && !string.IsNullOrEmpty(GetString(item, TitleAttribute)));
                var zoom = byTitle != 0 ? byTitle : afterMinimize;
                if (zoom != 0 && GetBool(zoom, EnabledAttribute) != false)
                {
                    return MacNative.CFRetain(zoom);
                }
            }

            return 0;
        }
        finally
        {
            Cf.Release(bar);
        }
    }

    private static bool IsMinimize(nint item) => string.Equals(GetString(item, MenuItemCmdCharAttribute), "M", StringComparison.OrdinalIgnoreCase);

    private static bool HoldsMinimize(nint menu) =>
        Children(menu).Any(item => string.Equals(GetString(item, MenuItemCmdCharAttribute), "M", StringComparison.OrdinalIgnoreCase)
            && (GetNumber(item, MenuItemCmdModifiersAttribute) ?? 0) == 0);

    private static nint Find(nint menu, string key, long modifiers, List<string> seen, int depth)
    {
        foreach (var item in Children(menu))
        {
            var cmd = GetString(item, MenuItemCmdCharAttribute);
            var mods = GetNumber(item, MenuItemCmdModifiersAttribute) ?? 0;
            var enabled = GetBool(item, EnabledAttribute) != false;
            seen.Add($"{GetString(item, TitleAttribute)} {cmd}/{mods}{(enabled ? string.Empty : " (disabled)")}");
            if (enabled && string.Equals(cmd, key, StringComparison.OrdinalIgnoreCase) && (mods & ~FunctionModifier) == modifiers)
            {
                return MacNative.CFRetain(item);
            }

            if (depth < MaxMenuDepth)
            {
                foreach (var submenu in Children(item))
                {
                    var found = Find(submenu, key, modifiers, seen, depth + 1);
                    if (found != 0)
                    {
                        return found;
                    }
                }
            }
        }

        return 0;
    }

    /// <summary>The element's children, borrowed: the array they live in is kept until <see cref="ReleaseMenuSearch"/>.</summary>
    private static List<nint> Children(nint element)
    {
        var children = new List<nint>();
        if (Copy(element, Cf.Constant(ChildrenAttribute), out var array) != MacNative.AXErrorSuccess || array == 0)
        {
            return children;
        }

        ChildArrays.Value!.Add(array);
        var count = MacNative.CFArrayGetCount(array);
        for (nint i = 0; i < count; i++)
        {
            children.Add(MacNative.CFArrayGetValueAtIndex(array, i));
        }

        return children;
    }

    /// <summary>Releases what <see cref="FindWindowMenuItem"/> copied; call once after using its result.</summary>
    public static void ReleaseMenuSearch()
    {
        foreach (var array in ChildArrays.Value!)
        {
            Cf.Release(array);
        }

        ChildArrays.Value!.Clear();
    }

    private static long? GetNumber(nint element, string attribute)
    {
        if (Copy(element, Cf.Constant(attribute), out var value) != MacNative.AXErrorSuccess)
        {
            return null;
        }

        try
        {
            return Cf.ReadInt64(value);
        }
        finally
        {
            Cf.Release(value);
        }
    }
}
