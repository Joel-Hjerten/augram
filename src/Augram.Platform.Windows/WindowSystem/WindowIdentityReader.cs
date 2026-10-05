using Augram.Core.Abstractions;

namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// Builds a <see cref="WindowIdentity"/> from a window handle using only <see cref="IWin32Windows"/> queries:
/// class chain (own, parent, root, root owner; distinct and non-empty), process identity through the UWP rule,
/// title and rectangles from the root owner, then the desktop and full-screen flags.
/// </summary>
internal sealed class WindowIdentityReader
{
    private const string UnknownProcess = "unknown";

    private readonly IWin32Windows _win;

    public WindowIdentityReader(IWin32Windows win)
    {
        _win = win;
    }

    public WindowIdentity? Read(nint handle)
    {
        if (handle == 0)
        {
            return null;
        }

        var root = Or(_win.Root(handle), handle);
        var rootOwner = Or(_win.RootOwner(handle), root);
        var classChain = ClassChain(handle, root, rootOwner);
        var rootClass = _win.ClassName(rootOwner);

        var processWindow = UwpHostRule.ProcessWindow(_win, handle, root, _win.ClassName(root));
        var (_, processId) = _win.ThreadAndProcess(processWindow);
        var path = processId == 0 ? null : _win.ProcessImagePath(processId);
        var processName = ProcessName(path, processId);

        var title = _win.Title(rootOwner);
        var isDesktop = IsDesktop(rootOwner, classChain);
        var isFullScreen = !isDesktop && IsFullScreen(rootOwner, rootClass);

        return new WindowIdentity(
            handle,
            rootOwner,
            processName,
            path,
            title.Length == 0 ? null : title,
            classChain,
            processId,
            isFullScreen,
            isDesktop);
    }

    private IReadOnlyList<string> ClassChain(nint handle, nint root, nint rootOwner)
    {
        var chain = new List<string>(4);
        foreach (var hwnd in new[] { handle, _win.Parent(handle), root, rootOwner })
        {
            if (hwnd == 0)
            {
                continue;
            }

            var name = _win.ClassName(hwnd);
            if (name.Length > 0 && !chain.Contains(name, StringComparer.Ordinal))
            {
                chain.Add(name);
            }
        }

        return chain;
    }

    private string ProcessName(string? path, int processId)
    {
        if (path is not null)
        {
            var file = Path.GetFileName(path);
            if (file.Length > 0)
            {
                return file;
            }
        }

        var baseName = processId == 0 ? null : _win.ProcessBaseName(processId);
        return string.IsNullOrEmpty(baseName) ? UnknownProcess : baseName + ".exe";
    }

    private bool IsDesktop(nint rootOwner, IReadOnlyList<string> classChain)
        => rootOwner == _win.ShellWindow() || rootOwner == _win.DesktopWindow() || DesktopRule.IsDesktop(classChain);

    private bool IsFullScreen(nint rootOwner, string rootClass)
        => _win.TryWindowRect(rootOwner, out var window)
           && _win.TryMonitorRect(rootOwner, out var monitor)
           && FullScreenRule.IsFullScreen(window, monitor, rootClass);

    private static nint Or(nint value, nint fallback) => value == 0 ? fallback : value;
}
