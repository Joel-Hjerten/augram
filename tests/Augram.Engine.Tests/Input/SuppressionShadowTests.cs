using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// The hook-side decision must equal the machine's own Suppress/PassThrough for every state and event,
/// because the hook applies the shadow's answer before the worker has run the machine. Driven over
/// random but physically plausible sequences (like Core's PairingInvariantTests), with the stroke button,
/// the capture-allowed verdict and the ignore key all varying.
/// </summary>
public sealed class SuppressionShadowTests
{
    private const int Sequences = 2000;
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();

    [Fact]
    public void RandomSequences_ShadowDecisionEqualsMachineDecision_InEveryState()
    {
        var rng = new Random(20261006);
        var statesSeen = new HashSet<(CaptureState, RawInputKind)>();
        var suppressed = 0;

        for (var sequence = 0; sequence < Sequences; sequence++)
        {
            var strokeButton = Buttons[rng.Next(Buttons.Length)];
            var machine = new CaptureStateMachine(strokeButton, new CaptureThresholds(CancelDelayMs: rng.Next(50, 600)));
            var shadow = new SuppressionShadow();

            foreach (var (raw, allowed, ignore) in Generate(rng))
            {
                if (rng.Next(25) == 0)
                {
                    // A live-save button change, applied to the machine; the shadow only ever reads the configured button.
                    machine.StrokeButton = Buttons[rng.Next(Buttons.Length)];
                }

                var stateBefore = machine.State;
                var shadowSays = shadow.Decide(in raw, stateBefore, machine.StrokeButton, allowed, ignore);
                var outcomes = machine.Handle(ToCaptureEvent(raw, allowed, ignore));
                var machineSays = outcomes.Any(o => o is CaptureOutcome.Suppress);

                Assert.True(shadowSays == machineSays, $"sequence {sequence}: {raw.Kind} {raw.Button} in {stateBefore}: shadow {shadowSays}, machine {machineSays}");
                statesSeen.Add((stateBefore, raw.Kind));
                suppressed += shadowSays ? 1 : 0;
                Assert.Equal(machine.State != CaptureState.Idle, shadow.Owed.HasValue);
            }
        }

        // HandedBack needs an anchor besides the stroke button: ChordPairingTests covers it.
        foreach (var state in Enum.GetValues<CaptureState>().Where(state => state != CaptureState.HandedBack))
        {
            foreach (var kind in new[] { RawInputKind.ButtonDown, RawInputKind.ButtonUp, RawInputKind.Wheel })
            {
                Assert.Contains((state, kind), statesSeen);
            }
        }

        Assert.True(suppressed > 1000, $"only {suppressed} suppressed events; the generator is not exercising capture");
    }

    [Fact]
    public void PressNotAllowed_PassesThrough_AndTheReleaseToo()
    {
        var shadow = new SuppressionShadow();
        Assert.False(shadow.Decide(RawInput.ButtonDown(MouseButton.Right, 0, 0, 0), CaptureState.Idle, MouseButton.Right, captureAllowed: false, ignoreKeyHeld: false));
        Assert.Null(shadow.Owed);
        Assert.False(shadow.Decide(RawInput.ButtonUp(MouseButton.Right, 0, 0, 1), CaptureState.Idle, MouseButton.Right, false, false));
    }

    [Fact]
    public void ButtonChangeMidCapture_KeepsConsumingTheOldButton()
    {
        var shadow = new SuppressionShadow();
        Assert.True(shadow.Decide(RawInput.ButtonDown(MouseButton.Right, 0, 0, 0), CaptureState.Idle, MouseButton.Right, true, false));
        Assert.False(shadow.Decide(RawInput.ButtonDown(MouseButton.Middle, 0, 0, 1), CaptureState.Held, MouseButton.Middle, true, false));
        Assert.True(shadow.Decide(RawInput.ButtonUp(MouseButton.Right, 0, 0, 2), CaptureState.Cancelled, MouseButton.Middle, false, false));
        Assert.Null(shadow.Owed);
        Assert.True(shadow.Decide(RawInput.ButtonDown(MouseButton.Middle, 0, 0, 3), CaptureState.Idle, MouseButton.Middle, true, false));
    }

    [Fact]
    public void RestoreAndReset_ChangeTheOwedButton()
    {
        var shadow = new SuppressionShadow();
        var empty = shadow.Save();
        shadow.Decide(RawInput.ButtonDown(MouseButton.Left, 0, 0, 0), CaptureState.Idle, MouseButton.Left, true, false);
        Assert.Equal(MouseButton.Left, shadow.Owed);
        Assert.Equal(HeldButtons.Left, shadow.OwedButtons);
        var pressed = shadow.Save();
        shadow.Restore(empty);
        Assert.Null(shadow.Owed);
        Assert.Equal(HeldButtons.None, shadow.OwedButtons);
        shadow.Restore(pressed);
        Assert.Equal(MouseButton.Left, shadow.Owed);
        shadow.Reset();
        Assert.Null(shadow.Owed);
        Assert.Equal(HeldButtons.None, shadow.OwedButtons);
    }

    private static CaptureEvent ToCaptureEvent(RawInput raw, bool allowed, bool ignore) => raw.Kind switch
    {
        RawInputKind.ButtonDown => new CaptureEvent.ButtonDown(raw.Button, raw.X, raw.Y, raw.TimestampMs, allowed, ignore),
        RawInputKind.ButtonUp => new CaptureEvent.ButtonUp(raw.Button, raw.X, raw.Y, raw.TimestampMs),
        RawInputKind.Move => new CaptureEvent.Move(raw.X, raw.Y, raw.TimestampMs),
        RawInputKind.Wheel => new CaptureEvent.Wheel(raw.Wheel, raw.X, raw.Y, raw.TimestampMs),
        _ => new CaptureEvent.Tick(raw.TimestampMs),
    };

    /// <summary>Random events, buttons alternating down/up, time monotonic; a Tick is encoded as <see cref="RawInputKind.KeyDown"/> (the shadow ignores keys).</summary>
    private static List<(RawInput Raw, bool Allowed, bool Ignore)> Generate(Random rng)
    {
        var events = new List<(RawInput, bool, bool)>();
        var isDown = new bool[Buttons.Length];
        long t = 0;
        int x = rng.Next(0, 1000), y = rng.Next(0, 1000);
        var count = rng.Next(5, 80);

        for (var i = 0; i < count; i++)
        {
            t += rng.Next(0, 300);
            var button = Buttons[rng.Next(Buttons.Length)];
            switch (rng.Next(7))
            {
                case 0 or 1 when !isDown[(int)button]:
                    isDown[(int)button] = true;
                    events.Add((RawInput.ButtonDown(button, x, y, t), rng.Next(10) != 0, rng.Next(10) == 0));
                    break;
                case 2 when isDown[(int)button]:
                    isDown[(int)button] = false;
                    events.Add((RawInput.ButtonUp(button, x, y, t), true, false));
                    break;
                case 3 or 4:
                    x += rng.Next(-40, 41);
                    y += rng.Next(-40, 41);
                    events.Add((RawInput.Move(x, y, t), true, false));
                    break;
                case 5:
                    events.Add((RawInput.WheelTick(rng.Next(2) == 0 ? WheelDirection.Up : WheelDirection.Down, x, y, t), true, false));
                    break;
                default:
                    events.Add((RawInput.KeyDown(KeyCode.None, t), true, false));
                    break;
            }
        }

        foreach (var button in Buttons.Where(b => isDown[(int)b]))
        {
            events.Add((RawInput.ButtonUp(button, x, y, ++t), true, false));
        }

        return events;
    }
}
