using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// A19 with trigger combinations (Joel, 2026-10-09: anchors per app, every suppressed down has a suppressed up, for every
/// button, through every state change). The real <see cref="InputGate"/> decides on random sequences of buttons, wheel ticks,
/// moves and Ctrl/Alt/Shift/Win keys while the pointer's answer (a random anchor plan and the ignore bits, as the watch
/// publishes per window), the tray toggle and the stroke button change between any two events; this test plays the worker:
/// it runs a <see cref="CaptureStateMachine"/> on what the gate posted, checks the hook's decision equals the machine's, and
/// applies what the worker would inject (a hand-back's down, its release, replayed clicks with their keys) to a model of the
/// OS. Proven, per sequence: a button's release gets its press's decision, the OS never sees an up for a button it does not
/// hold, keys pair the same way, and when every physical button and key is up again the OS holds none of them.
/// </summary>
public sealed class ChordPairingTests
{
    private const int Sequences = 3000;
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();
    private static readonly KeyCode[] Keys = [KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.LeftMeta];

    [Fact]
    public void RandomSequences_PairEveryButtonAndKey_AndLeaveTheOsHoldingNothing()
    {
        var rng = new Random(20261009);
        var seen = new HashSet<(CaptureState, RawInputKind)>();
        var counts = new Counts();

        for (var sequence = 0; sequence < Sequences; sequence++)
        {
            var strokeButton = Buttons[rng.Next(Buttons.Length)];
            var queue = Channel.CreateUnbounded<WorkerMessage>();
            var gate = new InputGate(queue.Writer, NullEventLog.Instance, strokeButton, KeyModifiers.Control, enabled: true);
            var machine = new CaptureStateMachine(strokeButton, new CaptureThresholds(CancelDelayMs: rng.Next(50, 600)));
            var os = new OsModel(sequence);
            var press = new bool?[Buttons.Length];
            var keyPress = new Dictionary<KeyCode, bool>();

            foreach (var raw in Generate(rng))
            {
                Vary(rng, gate, machine);
                var stateBefore = gate.State;
                var suppressed = gate.Handle(in raw);
                Drain(queue.Reader, gate, machine, os, sequence, counts);
                seen.Add((stateBefore, raw.Kind));
                os.Physical(raw, suppressed);
                Pair(raw, suppressed, press, keyPress, sequence, counts);
            }

            os.AssertNothingHeld();
            Assert.Equal(CaptureState.Idle, machine.State);
        }

        foreach (var state in Enum.GetValues<CaptureState>())
        {
            Assert.Contains((state, RawInputKind.ButtonUp), seen);
        }

        Assert.Contains((CaptureState.HandedBack, RawInputKind.ButtonDown), seen);
        Assert.True(counts.HandedBack > 300, $"only {counts.HandedBack} hand-backs");
        Assert.True(counts.Joined > 300, $"only {counts.Joined} buttons joined a press");
        Assert.True(counts.KeysClaimed > 300, $"only {counts.KeysClaimed} keys claimed by a press");
        Assert.True(counts.AnchorPresses > 300, $"only {counts.AnchorPresses} presses of an anchor other than the stroke button");
        Assert.True(counts.KeyPairs > 1000, $"only {counts.KeyPairs} key presses released");
    }

    /// <summary>Between events: a new window under the pointer (plan and ignore bits), the tray toggle, the stroke button (applied as the worker applies it).</summary>
    private static void Vary(Random rng, InputGate gate, CaptureStateMachine machine)
    {
        if (rng.Next(3) == 0)
        {
            gate.PublishPointer(rng.Next(6) == 0 ? rng.Next(1, 4) : 0, RandomPlan(rng));
        }

        if (rng.Next(25) == 0)
        {
            gate.Enabled = !gate.Enabled;
        }

        if (rng.Next(40) == 0)
        {
            var button = Buttons[rng.Next(Buttons.Length)];
            machine.StrokeButton = button;
            gate.PublishStrokeButton(button);
            gate.PublishState(machine.State);
        }
    }

    private static AnchorPlan RandomPlan(Random rng)
    {
        var plan = AnchorPlan.None;
        foreach (var button in Buttons)
        {
            if (rng.Next(3) == 0)
            {
                plan = plan.WithAnchor(button);
            }

            plan = plan.WithExtras(button, ownerIsStroke: rng.Next(2) == 0, (HeldButtons)(rng.Next(32) << 1));
        }

        return plan;
    }

    /// <summary>The worker's half: run the machine, check the hook agreed, apply what the worker injects, publish the state back.</summary>
    private static void Drain(ChannelReader<WorkerMessage> reader, InputGate gate, CaptureStateMachine machine, OsModel os, int sequence, Counts counts)
    {
        while (reader.TryRead(out var message))
        {
            if (message.Kind != WorkerMessage.MessageKind.Input)
            {
                continue;
            }

            var e = message.Event!;
            if (e is CaptureEvent.ButtonDown down && down.Plan.IsAnchor(down.Button) && down.Button != machine.StrokeButton && message.HookSuppressed)
            {
                counts.AnchorPresses++;
            }

            if (e is CaptureEvent.ButtonDown { } other && machine.State is CaptureState.Held or CaptureState.Drawing && other.Button != machine.ActiveButton && message.HookSuppressed)
            {
                counts.Joined++;
            }

            if (e is CaptureEvent.Key { Consumed: true })
            {
                counts.KeysClaimed++;
            }

            var outcomes = machine.Handle(e);
            if (e is CaptureEvent.ButtonDown or CaptureEvent.ButtonUp or CaptureEvent.Wheel)
            {
                var machineSays = outcomes.Any(outcome => outcome is CaptureOutcome.Suppress);
                Assert.True(machineSays == message.HookSuppressed, $"sequence {sequence}: {e}: hook {message.HookSuppressed}, machine {machineSays}");
            }

            foreach (var outcome in outcomes)
            {
                counts.HandedBack += outcome is CaptureOutcome.HandBack ? 1 : 0;
                os.Injected(outcome);
            }

            gate.PublishState(machine.State);
        }
    }

    /// <summary>A19 per button and per key: a release gets its press's decision; key repeats follow their press.</summary>
    private static void Pair(RawInput raw, bool suppressed, bool?[] press, Dictionary<KeyCode, bool> keyPress, int sequence, Counts counts)
    {
        switch (raw.Kind)
        {
            case RawInputKind.ButtonDown:
                press[(int)raw.Button] = suppressed;
                break;
            case RawInputKind.ButtonUp when press[(int)raw.Button] is { } pressed:
                Assert.True(pressed == suppressed, $"sequence {sequence}: {raw.Button} pressed {(pressed ? "consumed" : "passed")}, released {(suppressed ? "consumed" : "passed")}");
                press[(int)raw.Button] = null;
                break;
            case RawInputKind.KeyDown when keyPress.TryGetValue(raw.Key, out var first):
                Assert.True(first == suppressed, $"sequence {sequence}: a repeat of {raw.Key} did not follow its press");
                break;
            case RawInputKind.KeyDown:
                keyPress[raw.Key] = suppressed;
                break;
            case RawInputKind.KeyUp when keyPress.Remove(raw.Key, out var pressedKey):
                Assert.True(pressedKey == suppressed, $"sequence {sequence}: {raw.Key} pressed {(pressedKey ? "consumed" : "passed")}, released {(suppressed ? "consumed" : "passed")}");
                counts.KeyPairs++;
                break;
        }
    }

    /// <summary>Plausible input over at most 2 s (key records never age out): buttons and keys alternate down and up, keys repeat, mouse events carry the keys held; everything released at the end.</summary>
    private static List<RawInput> Generate(Random rng)
    {
        var events = new List<RawInput>();
        var isDown = new bool[Buttons.Length];
        var keyDown = new bool[Keys.Length];
        long t = 0;
        int x = rng.Next(0, 1000), y = rng.Next(0, 1000);
        var count = rng.Next(5, 70);

        KeyModifiers Held()
        {
            var held = KeyModifiers.None;
            for (var i = 0; i < Keys.Length; i++)
            {
                held |= keyDown[i] ? KeySuppressionShadowModifier(Keys[i]) : KeyModifiers.None;
            }

            return held;
        }

        for (var i = 0; i < count; i++)
        {
            t += rng.Next(0, 28);
            var button = Buttons[rng.Next(Buttons.Length)];
            var key = rng.Next(Keys.Length);
            switch (rng.Next(9))
            {
                case 0 or 1 when !isDown[(int)button]:
                    isDown[(int)button] = true;
                    events.Add(RawInput.ButtonDown(button, x, y, t, Held()));
                    break;
                case 2 when isDown[(int)button]:
                    isDown[(int)button] = false;
                    events.Add(RawInput.ButtonUp(button, x, y, t, Held()));
                    break;
                case 3 or 4:
                    x += rng.Next(-40, 41);
                    y += rng.Next(-40, 41);
                    events.Add(RawInput.Move(x, y, t));
                    break;
                case 5:
                    events.Add(RawInput.WheelTick(rng.Next(2) == 0 ? WheelDirection.Up : WheelDirection.Down, x, y, t, Held()));
                    break;
                case 6 or 7:
                    keyDown[key] = true;
                    events.Add(RawInput.KeyDown(Keys[key], t, Held()));
                    break;
                default:
                    if (keyDown[key])
                    {
                        keyDown[key] = false;
                        events.Add(RawInput.KeyUp(Keys[key], t, Held()));
                    }

                    break;
            }
        }

        foreach (var button in Buttons.Where(b => isDown[(int)b]))
        {
            events.Add(RawInput.ButtonUp(button, x, y, ++t));
        }

        for (var i = 0; i < Keys.Length; i++)
        {
            if (keyDown[i])
            {
                keyDown[i] = false;
                events.Add(RawInput.KeyUp(Keys[i], ++t));
            }
        }

        return events;
    }

    private static KeyModifiers KeySuppressionShadowModifier(KeyCode key) => Engine.Input.KeySuppressionShadow.ModifierOf(key);

    private sealed class Counts
    {
        public int HandedBack;
        public int Joined;
        public int KeysClaimed;
        public int AnchorPresses;
        public int KeyPairs;
    }
}
