using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// The Objective-C side of <see cref="MacNative"/>: cached classes and selectors, the <c>NSRect</c> return that differs
/// between arm64 and x86-64, and an autorelease pool for calls made off the main thread (the command executor has
/// none, so autoreleased results would otherwise pile up for the life of the thread).
/// </summary>
[SupportedOSPlatform("macos")]
internal static class ObjC
{
    private const string AppKitLibrary = "/System/Library/Frameworks/AppKit.framework/AppKit";

    private static readonly ConcurrentDictionary<string, nint> Selectors = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, nint> Classes = new(StringComparer.Ordinal);

    public static nint Selector(string name) => Selectors.GetOrAdd(name, MacNative.sel_registerName);

    /// <summary>Zero when the class is not loaded (AppKit is absent in a process without a UI).</summary>
    public static nint Class(string name) => Classes.GetOrAdd(name, MacNative.objc_getClass);

    /// <summary>
    /// AppKit's exported <c>NSString</c> constant named <paramref name="symbol"/> (a notification name) when it has one, else a
    /// constant string with the same text (names such as the screen-lock notifications have no symbol; the values are the text).
    /// </summary>
    public static nint AppKitString(string symbol)
    {
        if (NativeLibrary.TryLoad(AppKitLibrary, out var appKit) && NativeLibrary.TryGetExport(appKit, symbol, out var address) && Marshal.ReadIntPtr(address) is var value and not 0)
        {
            return value;
        }

        return Cf.Constant(symbol);
    }

    public static bool RespondsTo(nint receiver, string selector) =>
        receiver != 0 && MacNative.SendBool(receiver, Selector("respondsToSelector:"), Selector(selector)) != 0;

    public static MacNative.CGRect Rect(nint receiver, string selector)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            MacNative.SendRectStret(out var result, receiver, Selector(selector));
            return result;
        }

        return MacNative.SendRect(receiver, Selector(selector));
    }

    public static AutoreleasePool Pool() => new(MacNative.objc_autoreleasePoolPush());

    public readonly struct AutoreleasePool(nint token) : IDisposable
    {
        public void Dispose() => MacNative.objc_autoreleasePoolPop(token);
    }
}
