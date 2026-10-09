using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// CoreFoundation helpers over <see cref="MacNative"/>: constant strings for dictionary keys and AX attribute names
/// (created once, never released, like the framework's own <c>CFSTR</c> constants), reading strings, numbers and
/// booleans out of borrowed references, and <see cref="Release"/> that tolerates null.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class Cf
{
    private static readonly ConcurrentDictionary<string, nint> Constants = new(StringComparer.Ordinal);
    private static readonly Lazy<nint> TrueValue = new(() => Export("kCFBooleanTrue"));
    private static readonly Lazy<nint> FalseValue = new(() => Export("kCFBooleanFalse"));
    private static readonly Lazy<nuint> StringType = new(MacNative.CFStringGetTypeID);
    private static readonly Lazy<nuint> BooleanType = new(MacNative.CFBooleanGetTypeID);

    public static nint True => TrueValue.Value;

    public static nint False => FalseValue.Value;

    /// <summary>A <c>CFStringRef</c> for <paramref name="text"/> that lives for the process; for keys and attribute names.</summary>
    public static nint Constant(string text) =>
        Constants.GetOrAdd(text, static t => MacNative.CFStringCreateWithCString(0, t, MacNative.CFStringEncodingUtf8));

    /// <summary>A new <c>CFStringRef</c> (toll-free an <c>NSString</c>) for <paramref name="text"/>; owned, so <see cref="Release"/> it.</summary>
    public static nint String(string text) => MacNative.CFStringCreateWithCString(0, text, MacNative.CFStringEncodingUtf8);

    public static void Release(nint cf)
    {
        if (cf != 0)
        {
            MacNative.CFRelease(cf);
        }
    }

    /// <summary>The string behind a borrowed reference; null when it is null or not a string.</summary>
    public static string? ReadString(nint cf)
    {
        if (cf == 0 || MacNative.CFGetTypeID(cf) != StringType.Value)
        {
            return null;
        }

        var length = MacNative.CFStringGetLength(cf);
        var size = (int)MacNative.CFStringGetMaximumSizeForEncoding(length, MacNative.CFStringEncodingUtf8) + 1;
        var buffer = size <= 1024 ? stackalloc byte[size] : new byte[size];
        if (MacNative.CFStringGetCString(cf, buffer, size, MacNative.CFStringEncodingUtf8) == 0)
        {
            return null;
        }

        var end = buffer.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? buffer : buffer[..end]);
    }

    public static bool? ReadBool(nint cf) =>
        cf != 0 && MacNative.CFGetTypeID(cf) == BooleanType.Value ? MacNative.CFBooleanGetValue(cf) != 0 : null;

    public static long? ReadInt64(nint number) =>
        number != 0 && MacNative.CFNumberGetInt64(number, MacNative.CFNumberSInt64Type, out var value) != 0 ? value : null;

    public static double? ReadDouble(nint number) =>
        number != 0 && MacNative.CFNumberGetDouble(number, MacNative.CFNumberDoubleType, out var value) != 0 ? value : null;

    /// <summary>The value for a constant key in a borrowed <c>CFDictionaryRef</c>; borrowed as well.</summary>
    public static nint Get(nint dictionary, string key) => MacNative.CFDictionaryGetValue(dictionary, Constant(key));

    /// <summary>Reads a global <c>CFTypeRef</c> constant such as <c>kCFBooleanTrue</c>: the export is the variable's address.</summary>
    private static nint Export(string symbol)
    {
        var library = NativeLibrary.Load(MacNative.CoreFoundationLibrary);
        return Marshal.ReadIntPtr(NativeLibrary.GetExport(library, symbol));
    }
}
