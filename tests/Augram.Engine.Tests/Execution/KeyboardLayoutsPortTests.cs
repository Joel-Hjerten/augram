using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Augram.Engine.Execution;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>
/// The executor hands <see cref="EnginePorts.Layouts"/> to a Hotkey step with layout shortcuts (plan 0011), which reads the
/// active layout once and presses that layout's shortcut; the port here is the shared fake, so nothing asks the OS.
/// </summary>
public sealed class KeyboardLayoutsPortTests
{
    [Fact]
    public void TheLayoutsPortReachesAHotkeyStep_WhichPressesTheActiveLayoutsShortcut()
    {
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc);
        var simulator = new FakeInputSimulator();
        var ports = new EnginePorts { Input = new FakeInputSource(), Simulator = simulator, Layouts = layouts };
        var runner = new CommandRunner(ports, settleDelayMs: 0, CancellationToken.None);
        var back = new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket)
        {
            Layouts = new Dictionary<string, HotkeyBinding> { [FakeKeyboardLayouts.Swedish.Id] = new(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8) },
        };
        // Authored where the test runs: a converted step drops its layout shortcuts (plan 0011 decision 5).
        var command = Mappings.CommandHere("Back", Trigger.None, back);

        runner.Run(new ExecutionRequest(PressedTrigger.Of(Trigger.None), new CapturePoint(10, 10, 0), null), AppGroup.EmptyGlobal, command, target: null);

        Assert.Equal(["hotkey Alt, Meta+Digit8"], simulator.Keys);
        Assert.Equal(1, layouts.ActiveReads);
    }

    [Fact]
    public void TheDefaultPortIsTheNullObject()
    {
        Assert.Same(NullKeyboardLayouts.Instance, new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator() }.Layouts);
    }
}
