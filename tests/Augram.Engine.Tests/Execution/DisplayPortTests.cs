using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.DisplayMode;
using Augram.Engine.Execution;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>The executor hands <see cref="EnginePorts.DisplayModes"/> to the steps it runs (learnings 0002); the adapter here is a recorder.</summary>
public sealed class DisplayPortTests
{
    [Fact]
    public void TheDisplayModesPortReachesADisplayStep()
    {
        var displays = new RecordingDisplays();
        var ports = new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator(), DisplayModes = displays };
        var runner = new CommandRunner(ports, settleDelayMs: 0, CancellationToken.None);
        var command = Mappings.Command("Film", Trigger.None, new DisplayModeStep(Refresh: RefreshRate.FromHertz(24)));

        runner.Run(new ExecutionRequest(Trigger.None, new CapturePoint(10, 10, 0), null), AppGroup.EmptyGlobal, command, target: null);

        Assert.Equal(["3840×2160 at 24 Hz"], displays.Applied);
    }

    [Fact]
    public void TheDefaultPortIsTheNullObject()
    {
        Assert.Same(NullDisplayModes.Instance, new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator() }.DisplayModes);
    }

    private sealed class RecordingDisplays : IDisplayModes
    {
        private static readonly DisplayResolution Uhd = new(3840, 2160);

        public List<string> Applied { get; } = [];

        public HostPlatform Platform => HostPlatform.Windows;

        public bool CanSwitchHdr => false;

        public IReadOnlyList<DisplayInfo> Displays() =>
        [
            new("tv", "TV", new DisplayBounds(0, 0, 3840, 2160), true, new VideoMode(Uhd, RefreshRate.FromHertz(120)), [new VideoMode(Uhd, RefreshRate.FromHertz(120)), new VideoMode(Uhd, RefreshRate.FromHertz(24))]),
        ];

        public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode)
        {
            Applied.Add(mode.ToString());
            return DisplayChangeResult.Ok;
        }

        public DisplayChangeResult SetHdr(DisplayInfo display, bool on) => DisplayChangeResult.Failed("not in this test");
    }
}
