using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.ClearClipboard;
using Augram.Core.Steps.Scroll;
using Augram.Engine.Execution;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>
/// The executor hands <see cref="EnginePorts.Clipboard"/> to the Clear clipboard step, which runs right there (no UI
/// thread involved), and runs a Scroll at the gesture start through the simulator; both ports here are recorders, so
/// nothing touches the real clipboard or wheel.
/// </summary>
public sealed class ClipboardPortTests
{
    [Fact]
    public void TheClipboardPortReachesAClearClipboardStep_OnTheThreadThatRunsTheCommand()
    {
        var clipboard = new RecordingClipboard();
        var ports = new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator(), Clipboard = clipboard };
        var runner = new CommandRunner(ports, settleDelayMs: 0, CancellationToken.None);
        var command = Mappings.Command("Clear", Trigger.None, new ClearClipboardStep());

        runner.Run(new ExecutionRequest(PressedTrigger.Of(Trigger.None), new CapturePoint(10, 10, 0), null), AppGroup.EmptyGlobal, command, target: null);

        Assert.Equal([Environment.CurrentManagedThreadId], clipboard.ClearedOn);
    }

    [Fact]
    public void TheDefaultPortIsTheNullObject()
    {
        Assert.Same(NullClipboard.Instance, new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator() }.Clipboard);
    }

    [Fact]
    public void AScrollStepTurnsTheWheelAtTheGestureStartWithItsKeysHeld()
    {
        var simulator = new FakeInputSimulator();
        var ports = new EnginePorts { Input = new FakeInputSource(), Simulator = simulator };
        var runner = new CommandRunner(ports, settleDelayMs: 0, CancellationToken.None);
        var command = Mappings.Command("Zoom", Trigger.None, new ScrollStep(ScrollDirection.Up, 1, KeyModifiers.Control));

        runner.Run(new ExecutionRequest(PressedTrigger.Of(Trigger.None), new CapturePoint(30, 40, 0), null), AppGroup.EmptyGlobal, command, target: null);

        Assert.Equal(["scroll Up x1@30,40"], simulator.Mouse);
        Assert.Equal(["press LeftControl", "release LeftControl"], simulator.Keys);
    }

    private sealed class RecordingClipboard : IClipboard
    {
        public List<int> ClearedOn { get; } = [];

        public ClipboardResult Clear()
        {
            ClearedOn.Add(Environment.CurrentManagedThreadId);
            return ClipboardResult.Ok;
        }
    }
}
