using System.Runtime.InteropServices;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// The keyboard-layout part of the macOS P/Invoke surface (plan 0011): Carbon's HIToolbox, Text Input Source Services for
/// the active layout and the installed ones, <c>UCKeyTranslate</c> for what a key prints under a layout, and the keyboard
/// type it needs. <c>TISCopy…</c> and <c>TISCreate…</c> results are owned by the caller and released with
/// <c>CFRelease</c>; <c>TISGetInputSourceProperty</c> borrows. The property-name constants are exported <c>CFStringRef</c>
/// globals, read by <see cref="Tis"/>. C <c>Boolean</c> travels as a byte, <c>UniCharCount</c> as <c>nuint</c>.
/// </summary>
internal static partial class MacNative
{
    private const string CarbonLibrary = "/System/Library/Frameworks/Carbon.framework/Carbon";

    /// <summary><c>kUCKeyActionDisplay</c>: what the key would print, asked without pressing it.</summary>
    public const ushort UCKeyActionDisplay = 3;

    /// <summary>The longest string <c>UCKeyTranslate</c> is given room for; a key prints one character, a ligature a few.</summary>
    public const int UCKeyTranslateMaxLength = 4;

    /// <summary>The keyboard layout the next keystroke lands in (never an input mode such as Japanese): owned, release it.</summary>
    [LibraryImport(CarbonLibrary)]
    public static partial nint TISCopyCurrentKeyboardLayoutInputSource();

    /// <summary>The input sources matching <paramref name="properties"/> (a <c>CFDictionaryRef</c>, zero for every one); enabled ones only unless <paramref name="includeAllInstalled"/>. Owned <c>CFArrayRef</c>, release it.</summary>
    [LibraryImport(CarbonLibrary)]
    public static partial nint TISCreateInputSourceList(nint properties, byte includeAllInstalled);

    /// <summary>A borrowed property value (a <c>CFStringRef</c> for the id, name and type; a <c>CFDataRef</c> for the layout data); zero when the source has none.</summary>
    [LibraryImport(CarbonLibrary)]
    public static partial nint TISGetInputSourceProperty(nint inputSource, nint propertyKey);

    /// <summary>The physical keyboard's type (<c>kbdType</c>), which a layout's tables are keyed by.</summary>
    [LibraryImport(CarbonLibrary)]
    public static partial byte LMGetKbdType();

    /// <summary>
    /// What <paramref name="virtualKeyCode"/> prints under the layout at <paramref name="keyLayoutPtr"/> (a <c>UCKeyboardLayout</c>,
    /// the bytes of <c>kTISPropertyUnicodeKeyLayoutData</c>) with <paramref name="modifierKeyState"/> held
    /// (<c>(flags >> 8) &amp; 0xFF</c>; zero for none): UTF-16 units into <paramref name="unicodeString"/>, their count in
    /// <paramref name="actualStringLength"/>. A dead key prints nothing and leaves its state in <paramref name="deadKeyState"/>.
    /// Zero is <c>noErr</c>.
    /// </summary>
    [LibraryImport(CarbonLibrary)]
    public static partial int UCKeyTranslate(
        nint keyLayoutPtr,
        ushort virtualKeyCode,
        ushort keyAction,
        uint modifierKeyState,
        uint keyboardType,
        uint keyTranslateOptions,
        ref uint deadKeyState,
        nuint maxStringLength,
        out nuint actualStringLength,
        Span<ushort> unicodeString);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFDataGetBytePtr(nint data);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFDataGetLength(nint data);
}
