using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// Text Input Source Services over <see cref="MacNative"/> (plan 0011): the property keys as HIToolbox exports them (the
/// constant's own <c>CFStringRef</c> when the export is there, else a constant string with the same text, which is what
/// each constant holds), a string property of a borrowed source, and <c>UCKeyTranslate</c> over a source's layout data
/// for one key with nothing held. The caller owns the sources (<c>TISCopy…</c>, <c>TISCreate…</c>) and releases them.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class Tis
{
    private const string CarbonLibrary = "/System/Library/Frameworks/Carbon.framework/Carbon";

    public const string InputSourceId = "kTISPropertyInputSourceID";
    public const string LocalizedName = "kTISPropertyLocalizedName";
    public const string InputSourceType = "kTISPropertyInputSourceType";
    public const string UnicodeKeyLayoutData = "kTISPropertyUnicodeKeyLayoutData";

    private static readonly Dictionary<string, nint> Keys = new(StringComparer.Ordinal);

    /// <summary>The string property <paramref name="symbol"/> names on <paramref name="source"/>; null when it has none or it is not a string.</summary>
    public static string? ReadString(nint source, string symbol) => Cf.ReadString(MacNative.TISGetInputSourceProperty(source, Key(symbol)));

    /// <summary>
    /// What <paramref name="virtualKey"/> prints under <paramref name="source"/>'s layout with no modifier held, as the keycap is
    /// asked (<c>kUCKeyActionDisplay</c>) for the keyboard in use (<c>LMGetKbdType</c>): null when the source has no layout data
    /// (an input mode), the call fails, or the key prints nothing there (a dead key: its state is discarded, never carried).
    /// </summary>
    public static string? Translate(nint source, ushort virtualKey)
    {
        var data = MacNative.TISGetInputSourceProperty(source, Key(UnicodeKeyLayoutData));
        if (data == 0 || MacNative.CFDataGetLength(data) == 0)
        {
            return null;
        }

        var layout = MacNative.CFDataGetBytePtr(data);
        if (layout == 0)
        {
            return null;
        }

        uint deadKeyState = 0;
        Span<ushort> units = stackalloc ushort[MacNative.UCKeyTranslateMaxLength];
        var status = MacNative.UCKeyTranslate(layout, virtualKey, MacNative.UCKeyActionDisplay, 0, MacNative.LMGetKbdType(), 0, ref deadKeyState, (nuint)units.Length, out var length, units);
        if (status != 0 || length == 0)
        {
            return null;
        }

        return new string(MemoryMarshal.Cast<ushort, char>(units[..(int)length]));
    }

    /// <summary>The key's exported <c>CFStringRef</c> (the address of the global holds it), else a constant with the same text, the <c>k</c> prefix dropped.</summary>
    private static nint Key(string symbol)
    {
        lock (Keys)
        {
            if (!Keys.TryGetValue(symbol, out var key))
            {
                key = NativeLibrary.TryLoad(CarbonLibrary, out var carbon) && NativeLibrary.TryGetExport(carbon, symbol, out var address) && Marshal.ReadIntPtr(address) is var value and not 0
                    ? value
                    : Cf.Constant(symbol[1..]);
                Keys[symbol] = key;
            }

            return key;
        }
    }
}
