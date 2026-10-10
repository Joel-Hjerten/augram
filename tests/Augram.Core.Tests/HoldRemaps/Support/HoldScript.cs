using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Tests.HoldRemaps.Support;

/// <summary>
/// Readable event sequences for <see cref="HoldRemapMachine"/> tests, with a clock and a pointer, in the style of the capture
/// tests' <c>EventScript</c>: <c>Hold().After(50).Down(Left).Up(Left).After(100).Release()</c>. Each event is fed at once;
/// <see cref="Work"/> is what the worker was asked to do so far, as short phrases ("press Ctrl + Middle", "tap Space",
/// "replay B down", "run Note"), and <see cref="Decisions"/> the input decisions in order.
/// </summary>
internal sealed class HoldScript
{
    private readonly HoldRemapEntry _entry;
    private readonly List<string> _work = [];
    private readonly List<string> _decisions = [];
    private long _now;
    private int _x = 100;
    private int _y = 100;

    public HoldScript(HoldRemapEntry? entry = null)
    {
        _entry = entry ?? Blender.Entry();
    }

    public HoldRemapMachine Machine { get; } = new();

    /// <summary>What the worker was asked to do, in order.</summary>
    public IReadOnlyList<string> Work => _work;

    /// <summary>"suppress" or "pass" per input event, in order.</summary>
    public IReadOnlyList<string> Decisions => _decisions;

    /// <summary>The decision of the last input event.</summary>
    public string Last => _decisions[^1];

    public HoldScript After(long ms)
    {
        _now += ms;
        return this;
    }

    public HoldScript At(int x, int y)
    {
        _x = x;
        _y = y;
        return this;
    }

    /// <summary>The hold key (Space) goes down and the hook claims it.</summary>
    public HoldScript Hold() => Feed(new HoldRemapEvent.HoldDown(_entry, _now));

    /// <summary>The hold key comes up.</summary>
    public HoldScript Release() => Feed(new HoldRemapEvent.HoldUp(_entry.HoldKey, _now));

    public HoldScript Down(MouseButton button) => Feed(new HoldRemapEvent.Button(button, IsDown: true, _x, _y, _now));

    public HoldScript Up(MouseButton button) => Feed(new HoldRemapEvent.Button(button, IsDown: false, _x, _y, _now));

    public HoldScript Click(MouseButton button) => Down(button).Up(button);

    public HoldScript Wheel(WheelDirection direction) => Feed(new HoldRemapEvent.Wheel(direction, _x, _y, _now));

    public HoldScript KeyDown(KeyCode key) => Feed(new HoldRemapEvent.Key(key, KeyPhase.Down, _now, _x, _y));

    public HoldScript KeyRepeat(KeyCode key) => Feed(new HoldRemapEvent.Key(key, KeyPhase.Repeat, _now, _x, _y));

    public HoldScript KeyUp(KeyCode key) => Feed(new HoldRemapEvent.Key(key, KeyPhase.Up, _now, _x, _y));

    public HoldScript Type(KeyCode key) => KeyDown(key).KeyUp(key);

    public HoldScript Reset() => Feed(new HoldRemapEvent.Reset(_now));

    /// <summary>The app in front changed away from the hold's (the hook saw a new plan).</summary>
    public HoldScript FocusMoved() => Feed(new HoldRemapEvent.FocusMoved(_now));

    public HoldScript Feed(HoldRemapEvent e)
    {
        foreach (var outcome in Machine.Handle(e))
        {
            switch (outcome)
            {
                case HoldRemapOutcome.Suppress:
                    _decisions.Add("suppress");
                    break;
                case HoldRemapOutcome.PassThrough:
                    _decisions.Add("pass");
                    break;
                default:
                    _work.Add(Describe(outcome));
                    break;
            }
        }

        return this;
    }

    /// <summary>Forgets the work and decisions so far (the state stays): for asserting one stretch of a script.</summary>
    public HoldScript Clear()
    {
        _work.Clear();
        _decisions.Clear();
        return this;
    }

    private string Describe(HoldRemapOutcome outcome) => outcome switch
    {
        HoldRemapOutcome.TapHoldKey tap => $"tap {HotkeyText.KeyName(tap.Key)}",
        HoldRemapOutcome.PressOutput press => $"press {press.Output.Describe(HostPlatform.Windows)}",
        HoldRemapOutcome.RepeatOutput repeat => $"repeat {repeat.Output.Describe(HostPlatform.Windows)}",
        HoldRemapOutcome.ReleaseOutput release => $"release {release.Output.Describe(HostPlatform.Windows)}",
        HoldRemapOutcome.WheelOutput wheel => $"turn {wheel.Output.Describe(HostPlatform.Windows)}",
        HoldRemapOutcome.ReplayKey replay => $"replay {HotkeyText.KeyName(replay.Key)} {replay.Phase.ToString().ToLowerInvariant()}",
        HoldRemapOutcome.RunSteps run => $"run {_entry.Bindings.Single(binding => binding.CommandId == run.CommandId).Name}",
        HoldRemapOutcome.HoldEnded ended => $"end {HotkeyText.KeyName(ended.HoldKey)}",
        _ => outcome.ToString(),
    };
}
