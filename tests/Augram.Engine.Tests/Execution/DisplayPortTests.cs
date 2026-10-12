using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.DisplayMode;
using Augram.Engine.Execution;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>The executor hands <see cref="EnginePorts.DisplayModes"/> to the steps it runs (learnings 0002); the adapter here is the shared fake.</summary>
public sealed class DisplayPortTests
{
    [Fact]
    public void TheDisplayModesPortReachesADisplayStep()
    {
        var uhd = new DisplayResolution(3840, 2160);
        var displays = new FakeDisplayModes { CanSwitchHdr = false };
        displays.Listed.Add(new("tv", "TV", new DisplayBounds(0, 0, 3840, 2160), true, new VideoMode(uhd, RefreshRate.FromHertz(120)), [new VideoMode(uhd, RefreshRate.FromHertz(120)), new VideoMode(uhd, RefreshRate.FromHertz(24))]));
        var ports = new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator(), DisplayModes = displays };
        var runner = new CommandRunner(ports, settleDelayMs: 0, CancellationToken.None);
        var command = Mappings.Command("Film", Trigger.None, new DisplayModeStep(Refresh: RefreshRate.FromHertz(24)));

        runner.Run(new ExecutionRequest(PressedTrigger.Of(Trigger.None), new CapturePoint(10, 10, 0), null), AppGroup.EmptyGlobal, command, target: null);

        Assert.Equal(["mode tv 3840×2160 at 24 Hz"], displays.Calls);
    }

    [Fact]
    public void TheDefaultPortIsTheNullObject()
    {
        Assert.Same(NullDisplayModes.Instance, new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator() }.DisplayModes);
    }
}
