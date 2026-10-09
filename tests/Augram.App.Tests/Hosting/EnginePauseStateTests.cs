using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>The tray's view of the ignore list's pause: it follows a started engine (over fakes, no hook) and clears when the engine stops.</summary>
public sealed class EnginePauseStateTests
{
    [Fact]
    public void FollowsTheEnginesPause_AndClearsWhenItStops()
    {
        // Names for both platforms: the engine matches on the one the null window operations name, which is the test runner's.
        var vmware = new IgnoredApp(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"], MacProcessNames = ["vmware.exe"] }, DisableEntirely: true);
        var windows = new FocusedWindow();
        var host = new EngineHost(
            new EnginePorts
            {
                Input = new FakeInputSource(),
                Simulator = new FakeInputSimulator(),
                Windows = windows,
                Mapping = () => new MappingDocument([AppGroup.EmptyGlobal], [vmware]),
            },
            () => [],
            () => RecognitionOptions.Default,
            new EngineHostOptions(HealthPollInterval: TimeSpan.FromHours(1)));
        var state = new EnginePauseState(action => action());
        var raised = new List<string?>();
        state.PropertyChanged += (_, _) => raised.Add(state.PausedBy);

        host.Start();
        state.Follow(host);
        windows.Focused = new WindowIdentity(0x60, 0x60, "vmware.exe", null, null, [], 7, false, false);
        host.MappingChanged();
        EngineFixture.WaitFor(() => state.PausedBy == "VMware", "the pause for VMware");

        host.Dispose();
        state.Dispose();

        Assert.Null(state.PausedBy);
        Assert.Equal("VMware", raised[0]);
        Assert.Null(raised[^1]);
    }

    /// <summary>Knows one focused window and nothing under the pointer.</summary>
    private sealed class FocusedWindow : IWindowSystem
    {
        private WindowIdentity? _focused;

        public WindowIdentity? Focused
        {
            get => Volatile.Read(ref _focused);
            set => Volatile.Write(ref _focused, value);
        }

        public WindowIdentity? WindowAt(int x, int y) => null;

        public WindowIdentity? Foreground() => Focused;

        public nint? WindowKeyAt(int x, int y) => 0;

        public nint? ForegroundKey() => Focused?.Handle ?? 0;

        public ActivationResult Activate(WindowIdentity target) => ActivationResult.NotNeeded;
    }
}
