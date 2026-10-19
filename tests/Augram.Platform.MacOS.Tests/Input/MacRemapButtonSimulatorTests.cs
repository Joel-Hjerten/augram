using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Platform.MacOS.Input;
using Augram.TestSupport;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Input;

/// <summary>
/// What the macOS remap simulator announces to the engine's ledger before each mouse event it posts (2026-10-19: the Mac takes
/// other programs' simulated input as real, so Augram's own must be announced): one press per <c>PressRemapButton</c>, one release
/// per <c>ReleaseRemapButton</c>, one move per <c>DragRemapButton</c> and per <c>RepostMove</c>, and nothing for the flagsChanged
/// events. Over the internal constructor, whose post stands in for <c>CGEventPost</c>: the events are built by CoreGraphics and
/// released, nothing is posted, so no hook, pointer or app sees anything. Runs on a Mac only (CoreGraphics builds the events) and
/// returns at once elsewhere; no permission is needed to build an event.
/// </summary>
public sealed class MacRemapButtonSimulatorTests
{
    [Fact]
    public void EachMouseEventPostedIsAnnouncedOnceFirst_FlagsChangedEventsAreNot()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var announced = new List<string>();
        var posted = 0;
        var inner = new FakeInputSimulator();
        var announce = new MacPostedInput(
            Press: button => announced.Add($"press {button} after {posted}"),
            Release: button => announced.Add($"release {button} after {posted}"),
            Move: () => announced.Add($"move after {posted}"));
        var simulator = new MacRemapButtonSimulator(inner, announce, _ => posted++);

        Assert.Equal(SimulationResult.Success, simulator.PressRemapButton(MouseButton.Middle, KeyModifiers.Shift | KeyModifiers.Control, 40, 50));
        Assert.Equal(SimulationResult.Success, simulator.DragRemapButton(MouseButton.Middle, 45, 50, 5, 0));
        Assert.Equal(SimulationResult.Success, simulator.ReleaseRemapButton(MouseButton.Middle));
        Assert.Equal(SimulationResult.Success, simulator.RepostMove(50, 50, 5, 0));

        // Ctrl and Shift down (2 posts), the down announced then posted, Shift and Ctrl up (2 posts); the drag; the up; the move.
        Assert.Equal(["press Middle after 2", "move after 5", "release Middle after 6", "move after 7"], announced);
        Assert.Equal(8, posted);
        Assert.Empty(inner.All);
    }

    [Fact]
    public void WithoutAnnouncementsItStillPosts()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var posted = 0;
        var simulator = new MacRemapButtonSimulator(new FakeInputSimulator(), announce: null, _ => posted++);

        Assert.Equal(SimulationResult.Success, simulator.PressRemapButton(MouseButton.Left, KeyModifiers.None, 1, 2));
        Assert.Equal(SimulationResult.Success, simulator.DragRemapButton(MouseButton.Left, 2, 2, 1, 0));

        Assert.Equal(2, posted);
    }
}
