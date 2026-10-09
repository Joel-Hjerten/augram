using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// The ignore list end to end through the host with fakes (its watch thread is real): over an ignored app the stroke
/// button, its drag and the wheel pass through untouched; while a "disable while focused" app has focus everything does;
/// a press and its release always get the same decision (A19), whatever the watch publishes in between.
/// </summary>
public sealed class IgnoreListWatchTests
{
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
    private static readonly WindowIdentity BlenderWindow = FakeWindowSystem.Identity("blender.exe", handle: 0x50);
    private static readonly WindowIdentity NotepadWindow = FakeWindowSystem.Identity("notepad.exe", handle: 0x10);
    private static readonly WindowIdentity VMwareWindow = FakeWindowSystem.Identity("vmware.exe", handle: 0x60);
    private const MouseButton Stroke = EngineHarness.StrokeButton;

    private static MappingDocument Mapping(params IgnoredApp[] ignored) => new([AppGroup.EmptyGlobal], ignored);

    [Fact]
    public void OverAnIgnoredApp_TheButtonItsDragAndTheWheelPassThrough()
    {
        using var harness = new EngineHarness(mapping: Mapping(Blender));
        harness.Windows.Window = BlenderWindow;
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is not null, "the pointer over Blender");

        Assert.False(harness.Down(Stroke, 100, 100, 10));
        for (var i = 1; i <= 20; i++)
        {
            harness.Move(100 + (i * 10), 100, 10 + (i * 10));
        }

        Assert.False(harness.Wheel(WheelDirection.Up, 300, 100, 220));
        Assert.False(harness.Up(Stroke, 300, 100, 230));
        harness.WaitForLog(LogSources.Ignore, "Pointer over an ignored app; the stroke button passes through");

        // Back over another app: the next stroke is captured as usual, and it is the only thing the engine saw.
        harness.Windows.Window = NotepadWindow;
        harness.Move(310, 100, 300);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is null, "the pointer to leave Blender");
        var (down, up) = harness.Stroke(200, 0, startMs: 400);
        Assert.True(down);
        Assert.True(up);
        harness.WaitForEvents(1);
        Assert.IsType<EngineEvent.GestureRecognized>(Assert.Single(harness.Events));
        Assert.Empty(harness.Simulator.Clicks);
        Assert.Equal(["begin", .. Enumerable.Repeat("extend", 20), "end"], harness.Trail.Calls);
        Assert.False(harness.Log.Has(LogSources.Capture, "Suppression decision mismatch"));
    }

    [Fact]
    public void APressPassedOverAnIgnoredApp_KeepsItsReleasePassed_AndAConsumedPressItsReleaseConsumed()
    {
        using var harness = new EngineHarness(mapping: Mapping(Blender));
        harness.Windows.Window = BlenderWindow;
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is not null, "the pointer over Blender");

        // Passed through over Blender; the pointer is over Notepad by the release, which still passes (A19).
        Assert.False(harness.Down(Stroke, 100, 100, 10));
        harness.Windows.Window = NotepadWindow;
        harness.Move(400, 100, 20);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is null, "the pointer to leave Blender");
        Assert.False(harness.Up(Stroke, 400, 100, 30));

        // Consumed over Notepad; Blender is under the pointer by the release, which is still consumed, and the click replayed.
        Assert.True(harness.Down(Stroke, 400, 100, 40));
        harness.Windows.Window = BlenderWindow;
        harness.Move(402, 100, 50);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is not null, "the pointer over Blender again");
        Assert.True(harness.Up(Stroke, 402, 100, 60));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");
        harness.WaitForLog(LogSources.Capture, "Click replayed");
        Assert.False(harness.Log.Has(LogSources.Capture, "Suppression decision mismatch"));
    }

    [Fact]
    public void WhileADisableWhileFocusedAppHasFocus_EverythingPassesThrough_AndThePauseIsLoggedAndRaised()
    {
        var pauses = new List<string?>();
        using var harness = new EngineHarness(mapping: Mapping(Blender, VMware));
        harness.Host.PauseChanged += (_, app) =>
        {
            lock (pauses)
            {
                pauses.Add(app?.Name);
            }
        };
        harness.Windows.Window = NotepadWindow;
        harness.Windows.ForegroundWindow = VMwareWindow;
        harness.Host.MappingChanged();
        EngineHarness.WaitFor(() => harness.Host.PausedBy is not null, "the pause for VMware");

        var (down, up) = harness.Stroke(200, 0);
        Assert.False(down);
        Assert.False(up);
        Assert.False(harness.Wheel(WheelDirection.Down, 100, 100, 300));
        harness.WaitForLog(LogSources.Ignore, "Paused while an ignored app has focus");
        Assert.Equal("VMware", harness.Host.PausedBy!.Name);

        harness.Windows.ForegroundWindow = NotepadWindow;
        EngineHarness.WaitFor(() => harness.Host.PausedBy is null, "the pause to end");
        harness.WaitForLog(LogSources.Ignore, "Resumed: the ignored app lost focus");
        Assert.True(harness.Stroke(200, 0, startMs: 1000).Down);
        harness.WaitForEvents(1);
        EngineHarness.WaitFor(() => Count(pauses) == 2, "both pause notices");
        lock (pauses)
        {
            Assert.Equal(["VMware", null], pauses);
        }
    }

    [Fact]
    public void AMappingChange_JudgesThePointerWithoutWaitingForAMove()
    {
        using var harness = new EngineHarness(mapping: Mapping());
        harness.Windows.Window = BlenderWindow;
        harness.Move(100, 100, 0);
        Assert.Null(harness.Host.IgnoredUnderPointer);

        harness.Mapping = Mapping(Blender);
        harness.Host.MappingChanged();

        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is not null, "the pointer over Blender");
        harness.WaitForLog(LogSources.Ignore, "Ignore list watched");
        Assert.False(harness.Down(Stroke, 100, 100, 10));
        Assert.False(harness.Up(Stroke, 100, 100, 20));

        harness.Mapping = Mapping(Blender with { IsActive = false });
        harness.Host.MappingChanged();
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is null, "Blender switched off");
        harness.WaitForLog(LogSources.Ignore, "Ignore list not watched");
        Assert.True(harness.Down(Stroke, 100, 100, 30));
        Assert.True(harness.Up(Stroke, 100, 100, 40));
    }

    [Fact]
    public void WithNoActiveIgnoredApp_NothingIsLookedUp()
    {
        using var harness = new EngineHarness(mapping: Mapping(Blender with { IsActive = false }));
        harness.Windows.Window = BlenderWindow;
        harness.Windows.ForegroundWindow = BlenderWindow;

        for (var i = 0; i < 20; i++)
        {
            harness.Move(100 + i, 100, i);
        }

        harness.Host.MappingChanged();
        Assert.True(harness.Stroke(200, 0, startMs: 100).Down);
        harness.WaitForEvents(1);
        Assert.Equal(0, harness.Windows.KeyLookups);
        Assert.Equal(0, harness.Windows.ForegroundKeyLookups);
    }

    [Fact]
    public void Stop_EndsThePause_AndSaysSo()
    {
        var pauses = new List<string?>();
        var harness = new EngineHarness(mapping: Mapping(VMware));
        harness.Host.PauseChanged += (_, app) =>
        {
            lock (pauses)
            {
                pauses.Add(app?.Name);
            }
        };
        harness.Windows.ForegroundWindow = VMwareWindow;
        harness.Host.MappingChanged();
        EngineHarness.WaitFor(() => harness.Host.PausedBy is not null, "the pause for VMware");

        harness.Dispose();

        Assert.Null(harness.Host.PausedBy);
        lock (pauses)
        {
            Assert.Equal(["VMware", null], pauses);
        }
    }

    private static int Count(List<string?> pauses)
    {
        lock (pauses)
        {
            return pauses.Count;
        }
    }
}
