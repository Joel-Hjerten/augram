using System.Runtime.Versioning;
using System.Text;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// Reads the window server's window list, the active displays and process paths. CoreGraphics only: no app is asked,
/// nothing blocks on a hung app, and no permission is needed except for window titles (Screen Recording), which are
/// null without it.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacWindowList
{
    private const int MaxDisplays = 16;

    /// <summary>Every on-screen window, desktop elements included, front to back.</summary>
    public static IReadOnlyList<MacWindowInfo> OnScreen() =>
        Read(MacNative.CGWindowListCopyWindowInfo(MacNative.CGWindowListOptionOnScreenOnly, 0));

    public static MacWindowInfo? ById(uint id) =>
        Read(MacNative.CGWindowListCopyWindowInfo(MacNative.CGWindowListOptionIncludingWindow, id)).FirstOrDefault(window => window.Id == id);

    /// <summary>Each active display's bounds in global top-left points.</summary>
    public static IReadOnlyList<MacRect> Displays()
    {
        Span<uint> ids = stackalloc uint[MaxDisplays];
        if (MacNative.CGGetActiveDisplayList(MaxDisplays, ids, out var count) != 0)
        {
            return [];
        }

        var displays = new List<MacRect>((int)count);
        foreach (var id in ids[..(int)Math.Min(count, MaxDisplays)])
        {
            var bounds = MacNative.CGDisplayBounds(id);
            displays.Add(new MacRect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }

        return displays;
    }

    /// <summary>The executable's full path, e.g. <c>/Applications/Safari.app/Contents/MacOS/Safari</c>; null when the OS will not say.</summary>
    public static string? ProcessPath(int pid)
    {
        Span<byte> buffer = stackalloc byte[MacNative.ProcPidPathInfoMaxSize];
        var length = MacNative.proc_pidpath(pid, buffer, MacNative.ProcPidPathInfoMaxSize);
        return length > 0 ? Encoding.UTF8.GetString(buffer[..length]) : null;
    }

    private static List<MacWindowInfo> Read(nint array)
    {
        if (array == 0)
        {
            return [];
        }

        try
        {
            var count = MacNative.CFArrayGetCount(array);
            var windows = new List<MacWindowInfo>((int)count);
            for (nint i = 0; i < count; i++)
            {
                if (ReadOne(MacNative.CFArrayGetValueAtIndex(array, i)) is { } window)
                {
                    windows.Add(window);
                }
            }

            return windows;
        }
        finally
        {
            Cf.Release(array);
        }
    }

    private static MacWindowInfo? ReadOne(nint entry)
    {
        var id = Cf.ReadInt64(Cf.Get(entry, "kCGWindowNumber"));
        var pid = Cf.ReadInt64(Cf.Get(entry, "kCGWindowOwnerPID"));
        var boundsEntry = Cf.Get(entry, "kCGWindowBounds");
        if (id is null || pid is null || boundsEntry == 0 || MacNative.CGRectMakeWithDictionaryRepresentation(boundsEntry, out var bounds) == 0)
        {
            return null;
        }

        return new MacWindowInfo(
            (uint)id.Value,
            (int)pid.Value,
            Cf.ReadString(Cf.Get(entry, "kCGWindowOwnerName")) ?? string.Empty,
            Cf.ReadString(Cf.Get(entry, "kCGWindowName")),
            (int)(Cf.ReadInt64(Cf.Get(entry, "kCGWindowLayer")) ?? 0),
            Cf.ReadDouble(Cf.Get(entry, "kCGWindowAlpha")) ?? 1,
            new MacRect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }
}
