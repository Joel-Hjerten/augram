using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;
using Augram.Platform.MacOS.WindowSystem;
using Xunit;

namespace Augram.Platform.MacOS.Tests.WindowSystem;

/// <summary>
/// The real Accessibility API against a real window: opens a throwaway TextEdit document and drives it through
/// <see cref="MacWindowSystem.WindowAt"/> and <see cref="MacWindowOperations"/> (maximize, restore, minimize, close).
/// Opt-in only with <c>AUGRAM_MAC_LIVE=1</c>: it moves a window on the desktop and needs the Accessibility permission
/// for the app running the tests. Returns at once anywhere else, CI included.
/// </summary>
public sealed class MacWindowOperationsLiveTests
{
    private const string Variable = "AUGRAM_MAC_LIVE";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public void TextEditWindow_MaximizeRestoreMinimizeClose()
    {
        if (!OperatingSystem.IsMacOS() || Environment.GetEnvironmentVariable(Variable) != "1")
        {
            return;
        }

        Run();
    }

    [SupportedOSPlatform("macos")]
    private static void Run()
    {
        // NSScreen lives in AppKit, which a test host has not loaded; the app has it through the UI toolkit.
        NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
        Assert.True(Ax.IsTrusted, "The app running the tests needs the Accessibility permission.");

        var name = $"augram-live-{Guid.NewGuid():N}.txt";
        var file = Path.Combine(Path.GetTempPath(), name);
        File.WriteAllText(file, "Augram live test window");
        try
        {
            using (var open = Process.Start("open", ["-a", "TextEdit", file]))
            {
                open.WaitForExit();
            }

            var (pid, id) = WaitForWindow(name);
            var info = MacWindowList.ById(id) ?? throw new InvalidOperationException("The window server does not list the window.");
            var target = new MacWindowSystem().WindowAt((int)info.Bounds.CenterX, (int)info.Bounds.CenterY);
            Assert.NotNull(target);
            Assert.Equal((nint)id, target.RootHandle);
            Assert.Equal("TextEdit", target.ProcessName);
            Assert.False(target.IsDesktop);

            var operations = new MacWindowOperations();
            var before = Frame(pid, id);

            Assert.Equal(WindowOperationResult.Ok, operations.Perform(WindowOperation.MaximizeOrRestore, target));
            var screens = MacScreens.All();
            var visible = screens[MacMaximize.ScreenFor(before, [.. screens.Select(s => s.Frame)])].Visible;
            Assert.True(MacMaximize.Fills(Frame(pid, id), visible), $"maximized {Frame(pid, id)} should fill {visible}");

            Assert.Equal(WindowOperationResult.Ok, operations.Perform(WindowOperation.MaximizeOrRestore, target));
            Assert.True(Frame(pid, id).IsNear(before, 2), $"restored {Frame(pid, id)} should be back at {before}");

            Assert.Equal(WindowOperationResult.Ok, operations.Perform(WindowOperation.Minimize, target));
            Assert.True(WithWindow(pid, id, window => Ax.GetBool(window, Ax.MinimizedAttribute)) == true);

            Assert.Equal(WindowOperationResult.Ok, operations.Perform(WindowOperation.Close, target));
            Assert.True(SpinWait.SpinUntil(() => MacWindowList.ById(id) is null, Patience), "the window should be gone after Close");
            Assert.Equal("window gone", operations.Perform(WindowOperation.Close, target).Reason);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [SupportedOSPlatform("macos")]
    private static (int Pid, uint Id) WaitForWindow(string title)
    {
        (int, uint)? found = null;
        SpinWait.SpinUntil(
            () =>
            {
                foreach (var window in MacWindowList.OnScreen().Where(w => w.OwnerName == "TextEdit" && w.Layer == MacWindowPick.NormalLayer))
                {
                    if (WithWindow(window.ProcessId, window.Id, element => Ax.GetString(element, Ax.TitleAttribute))?.Contains(title, StringComparison.Ordinal) == true)
                    {
                        found = (window.ProcessId, window.Id);
                        return true;
                    }
                }

                return false;
            },
            Patience);
        return found ?? throw new InvalidOperationException($"No TextEdit window titled {title} appeared.");
    }

    [SupportedOSPlatform("macos")]
    private static MacRect Frame(int pid, uint id) =>
        WithWindow(pid, id, Ax.Frame) ?? throw new InvalidOperationException("Could not read the window frame.");

    [SupportedOSPlatform("macos")]
    private static T? WithWindow<T>(int pid, uint id, Func<nint, T?> read)
    {
        var app = Ax.Application(pid);
        try
        {
            var window = Ax.FindWindow(app, id, out _);
            try
            {
                return window == 0 ? default : read(window);
            }
            finally
            {
                Cf.Release(window);
            }
        }
        finally
        {
            Cf.Release(app);
        }
    }
}
