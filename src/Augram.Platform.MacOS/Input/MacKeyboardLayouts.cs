using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The <see cref="IKeyboardLayouts"/> on macOS (plan 0011): Text Input Source Services.
/// <see cref="Active"/> is <c>TISCopyCurrentKeyboardLayoutInputSource</c> (the layout the next keystroke lands in, never an
/// input mode), <see cref="Installed"/> the enabled sources of <c>TISCreateInputSourceList</c> kept to keyboard layouts (what
/// the menu bar's input menu offers, in its order), each read as its id (<c>kTISPropertyInputSourceID</c>) and localized name
/// (<c>kTISPropertyLocalizedName</c>); <see cref="CharacterOf"/> is <c>UCKeyTranslate</c> over a layout's
/// <c>kTISPropertyUnicodeKeyLayoutData</c> for the key's virtual key (<see cref="MacVirtualKeys"/>), nothing held, as the
/// keycap is asked (a dead key prints its own mark, "¨"): the active layout for a null id, else the enabled source whose id is
/// the given one (<see cref="MacInputSources.HasId"/>; an id no enabled source has answers null). The TIS calls run on the
/// main thread (<see cref="MainThread"/>: inline from the UI thread, a hop from the command executor) inside an autorelease
/// pool, as <see cref="WindowSystem.MacScreens"/> reads <c>NSScreen</c>; when the hop fails (no main loop: a test host) every
/// member answers "cannot say" (null, empty). Every owned reference is released. <see cref="MacInputSources"/> (the id, name
/// and type rules) and <see cref="MacVirtualKeys"/> are pure and tested on every OS; the calls themselves are checked by Joel
/// on the Mac (the README's checks).
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacKeyboardLayouts : IKeyboardLayouts
{
    public KeyboardLayout? Active => MainThread.TryInvoke(ReadActive, out var layout) ? layout : null;

    public IReadOnlyList<KeyboardLayout> Installed => MainThread.TryInvoke(ReadInstalled, out var layouts) ? layouts : [];

    public string? CharacterOf(KeyCode key, string? layoutId = null)
        => MacVirtualKeys.TryMap(key, out var virtualKey) && MainThread.TryInvoke(() => ReadCharacter(virtualKey, layoutId), out var character) ? character : null;

    /// <summary>Main thread: the active layout as a <see cref="KeyboardLayout"/>; null when there is none or it has no id.</summary>
    private static KeyboardLayout? ReadActive()
    {
        using var pool = ObjC.Pool();
        var source = MacNative.TISCopyCurrentKeyboardLayoutInputSource();
        try
        {
            return source == 0 ? null : Read(source);
        }
        finally
        {
            Cf.Release(source);
        }
    }

    /// <summary>Main thread: the enabled keyboard layouts in the OS's order; empty when the list cannot be made.</summary>
    private static IReadOnlyList<KeyboardLayout> ReadInstalled()
    {
        using var pool = ObjC.Pool();
        var list = MacNative.TISCreateInputSourceList(properties: 0, includeAllInstalled: 0);
        if (list == 0)
        {
            return [];
        }

        try
        {
            var count = (int)MacNative.CFArrayGetCount(list);
            var layouts = new List<KeyboardLayout>(count);
            for (var i = 0; i < count; i++)
            {
                var source = MacNative.CFArrayGetValueAtIndex(list, i);
                if (source != 0 && MacInputSources.IsKeyboardLayout(Tis.ReadString(source, Tis.InputSourceType)) && Read(source) is { } layout)
                {
                    layouts.Add(layout);
                }
            }

            return layouts;
        }
        finally
        {
            Cf.Release(list);
        }
    }

    /// <summary>
    /// Main thread: what <paramref name="virtualKey"/> prints under the active layout (<paramref name="layoutId"/> null) or under
    /// the enabled source with that id; null when it prints nothing or there is no such layout.
    /// </summary>
    private static string? ReadCharacter(ushort virtualKey, string? layoutId)
    {
        using var pool = ObjC.Pool();
        if (layoutId is null)
        {
            var source = MacNative.TISCopyCurrentKeyboardLayoutInputSource();
            try
            {
                return source == 0 ? null : MacInputSources.Character(Tis.Translate(source, virtualKey));
            }
            finally
            {
                Cf.Release(source);
            }
        }

        var list = MacNative.TISCreateInputSourceList(properties: 0, includeAllInstalled: 0);
        if (list == 0)
        {
            return null;
        }

        try
        {
            var count = (int)MacNative.CFArrayGetCount(list);
            for (var i = 0; i < count; i++)
            {
                // The list borrows its sources; the list's release is theirs.
                var source = MacNative.CFArrayGetValueAtIndex(list, i);
                if (source != 0 && MacInputSources.HasId(Tis.ReadString(source, Tis.InputSourceId), layoutId))
                {
                    return MacInputSources.Character(Tis.Translate(source, virtualKey));
                }
            }

            return null;
        }
        finally
        {
            Cf.Release(list);
        }
    }

    private static KeyboardLayout? Read(nint source) => MacInputSources.Layout(Tis.ReadString(source, Tis.InputSourceId), Tis.ReadString(source, Tis.LocalizedName));
}
