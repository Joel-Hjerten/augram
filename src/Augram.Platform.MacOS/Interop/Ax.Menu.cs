namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// An app's menu bar through Accessibility, for MaximizeOrRestore's use of macOS's own window tiling (Joel, 2026-10-09:
/// "the green button's Fill"): Window › Move &amp; Resize › Fill and Return to Previous Size. Found without menu titles,
/// which are localised: the Window menu is the one holding Minimize (⌘M), and the tiling items are matched by their
/// key equivalents (a character with Control and no Command; the fn key has no Accessibility modifier bit).
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
            if (enabled && string.Equals(cmd, key, StringComparison.OrdinalIgnoreCase) && mods == modifiers)
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
