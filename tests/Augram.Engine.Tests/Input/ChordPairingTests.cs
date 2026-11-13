using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;
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
/// hold, keys pair the same way, and when every physical button and key is up again the OS holds none of them. Another
/// program's posted releases (plan 0005 decision 10) come in too, half of them swallowing the real release: the hook never
/// suppresses one, the press it owns ends in the hook and the machine alike, and the OS still ends holding nothing. Drag triggers
/// (plan 0015) too: some anchors are drag-bound, and the window's outputs hold a Remap button output for them (with or without a
/// held key), run ordinary steps, or match no press with the keys held; the test plays each drag as the worker does (the output's
/// held key and button down, nothing, or the owner handed back) and its end (the button up, then the held key; the owner up
/// unless another program released it), so the OS ends holding nothing there either.
/// </summary>
public sealed class ChordPairingTests
{
    private const int Sequences = 3000;

    // Plan 0015: sequences after the first 3,000 have drag-bound anchors and drag commands, at the short drag distance.
    private const int DragSequences = 3000;
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();
    private static readonly KeyCode[] Keys = [KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.LeftMeta];

    [Fact]
    public void RandomSequences_PairEveryButtonAndKey_AndLeaveTheOsHoldingNothing()
    {
        // The drag sequences draw from their own stream, after the others: those keep the shape their counts were calibrated on.
        var plainRng = new Random(20261009);
        var dragRng = new Random(20261113);
        var seen = new HashSet<(CaptureState, RawInputKind)>();
        var counts = new Counts();

        for (var sequence = 0; sequence < Sequences + DragSequences; sequence++)
        {
            var drags = sequence >= Sequences;
            var rng = drags ? dragRng : plainRng;
            var strokeButton = Buttons[rng.Next(Buttons.Length)];
            var queue = Channel.CreateUnbounded<WorkerMessage>();
            var gate = new InputGate(queue.Writer, NullEventLog.Instance, strokeButton, KeyModifiers.Control, enabled: true);
            // A long button drag distance in half the sequences keeps an anchor held while another button comes (and fires a chord).
            var machine = new CaptureStateMachine(strokeButton, new CaptureThresholds(CancelDelayMs: rng.Next(50, 600), ButtonDragDistancePx: sequence % 2 == 0 || drags ? 10 : 200));
            var os = new OsModel(sequence);
            var press = new bool?[Buttons.Length];
            var keyPress = new Dictionary<KeyCode, bool>();
            var worker = new DragWorker();

            foreach (var raw in Generate(rng))
            {
                Vary(rng, drags, gate, machine);
                var stateBefore = gate.State;
                var suppressed = gate.Handle(in raw);
                Drain(queue.Reader, gate, machine, os, worker, sequence, counts);
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
        Assert.Contains((CaptureState.DragFiring, RawInputKind.ButtonDown), seen);
        Assert.Contains((CaptureState.DragFiring, RawInputKind.ButtonReleasedElsewhere), seen);
        // Calibrated against the fixed seed (about 60% of what it reaches): a drag a command takes needs its anchor pressed with no
        // other button held, so most drags (a press with another button down) are handed back.
        Assert.True(counts.DragOutputs > 20, $"only {counts.DragOutputs} drag outputs held");
        Assert.True(counts.DragHeldKeys > 10, $"only {counts.DragHeldKeys} drag outputs with a held key");
        Assert.True(counts.DragSteps > 25, $"only {counts.DragSteps} drags that ran steps");
        Assert.True(counts.DragHandBacks > 150, $"only {counts.DragHandBacks} drags no command took, handed back");
        Assert.True(counts.HandedBack > 300, $"only {counts.HandedBack} hand-backs");
        Assert.True(counts.Joined > 300, $"only {counts.Joined} buttons joined a press");
        Assert.True(counts.KeysClaimed > 300, $"only {counts.KeysClaimed} keys claimed by a press");
        Assert.True(counts.AnchorPresses > 300, $"only {counts.AnchorPresses} presses of an anchor other than the stroke button");
        Assert.True(counts.KeyPairs > 1000, $"only {counts.KeyPairs} key presses released");
        Assert.True(counts.EndedElsewhere > 100, $"only {counts.EndedElsewhere} presses ended by a release elsewhere");
        // A chord needs an anchor held back, a plan that fires the next button, and no hand-back first: rarer than the rest.
        Assert.True(counts.Chords > 50, $"only {counts.Chords} button triggers fired");
    }

    /// <summary>Between events: a new window under the pointer (plan and ignore bits), the tray toggle, the stroke button (applied as the worker applies it).</summary>
    private static void Vary(Random rng, bool withDrags, InputGate gate, CaptureStateMachine machine)
    {
        if (rng.Next(3) == 0)
        {
            var ignore = rng.Next(6) == 0 ? rng.Next(1, 4) : 0;
            var plan = RandomPlan(rng);
            var (drags, outputs) = withDrags ? RandomDrags(rng, plan) : (AnchorDragDistances.None, ButtonOutputs.Empty);
            gate.PublishPointer(ignore, plan, drags, outputs);
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

            // Plan 0005: some extras of a physical anchor fire a button trigger at their press.
            for (var i = rng.Next(3); i > 0; i--)
            {
                var pressed = Buttons[rng.Next(Buttons.Length)];
                plan = pressed == button ? plan : plan.WithExtras(button, ownerIsStroke: false, pressed.Flag()).WithFires(button, pressed);
            }
        }

        return plan;
    }

    /// <summary>
    /// Plan 0015: some anchors of the plan are drag-bound, with a drag command over the window per set of keys held (the press's
    /// keys are not known yet) that holds a Remap button output (half of them with a held key), runs ordinary steps, or is absent,
    /// so no command takes a drag with those keys and the press is handed back.
    /// </summary>
    private static (AnchorDragDistances, ButtonOutputs) RandomDrags(Random rng, AnchorPlan plan)
    {
        var drags = AnchorDragDistances.None;
        var outputs = new List<ButtonOutput>();
        var steps = new List<Trigger>();
        foreach (var anchor in Buttons.Where(button => plan.IsAnchor(button) && rng.Next(4) != 0))
        {
            drags = drags.WithOptionsValue(anchor).WithDragBound(anchor);
            for (var keys = 0; keys < 16; keys++)
            {
                // Control 1, Alt 2, Shift 4, Meta 8: every set of the four.
                var trigger = Trigger.ForDrag(new TriggerHold(anchor.Flag(), (KeyModifiers)keys));
                switch (rng.Next(3))
                {
                    case 0:
                        steps.Add(trigger);
                        break;
                    case 1:
                        var output = new RemapOutput.Button(Buttons[rng.Next(Buttons.Length)]) { HeldKey = rng.Next(2) == 0 ? KeyCode.Space : KeyCode.None };
                        outputs.Add(new ButtonOutput(CommandId.New(), "Pan", trigger, output));
                        break;
                }
            }
        }

        return (drags, ButtonOutputs.Of(outputs, steps));
    }

    /// <summary>The worker's half: run the machine, check the hook agreed, apply what the worker injects, publish the state back.</summary>
    private static void Drain(ChannelReader<WorkerMessage> reader, InputGate gate, CaptureStateMachine machine, OsModel os, DragWorker worker, int sequence, Counts counts)
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

            // As the worker's NotePress: a new press of an anchor keeps the outputs of the window it went down over.
            if (e is CaptureEvent.ButtonDown pressed && machine.State == CaptureState.Held && machine.ActiveButton == pressed.Button)
            {
                worker.PressOutputs = message.Payload as ButtonOutputs ?? ButtonOutputs.Empty;
            }

            foreach (var outcome in outcomes)
            {
                counts.HandedBack += outcome is CaptureOutcome.HandBack ? 1 : 0;
                counts.EndedElsewhere += outcome is CaptureOutcome.Cancelled { Reason: CancelReason.ReleasedElsewhere } ? 1 : 0;
                counts.Chords += outcome is CaptureOutcome.ButtonTrigger ? 1 : 0;
                os.Injected(outcome);
                switch (outcome)
                {
                    case CaptureOutcome.DragTrigger drag:
                        worker.Fire(drag, os, counts);
                        break;
                    case CaptureOutcome.DragTriggerEnded ended:
                        os.DragEnded(ended.Button, e is CaptureEvent.ButtonReleasedElsewhere);
                        break;
                }
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
                held |= keyDown[i] ? HotkeyKeys.ModifierOf(Keys[i]) : KeyModifiers.None;
            }

            return held;
        }

        for (var i = 0; i < count; i++)
        {
            t += rng.Next(0, 28);
            var button = Buttons[rng.Next(Buttons.Length)];
            var key = rng.Next(Keys.Length);
            if (isDown[(int)button] && rng.Next(10) == 0)
            {
                // Another program posts its own release; half the time it swallows the real one, which then never comes.
                events.Add(RawInput.ButtonReleasedElsewhere(button, x, y, t));
                isDown[(int)button] = rng.Next(2) == 0;
                continue;
            }

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

    /// <summary>
    /// The worker's drag half (<c>EngineWorker.DragTriggers.cs</c>), what it decides from the press's outputs: the Remap button
    /// output that matches the press is held, a matching drag with ordinary steps holds nothing, and with neither the owner is
    /// handed back to the app.
    /// </summary>
    private sealed class DragWorker
    {
        public ButtonOutputs PressOutputs { get; set; } = ButtonOutputs.Empty;

        public void Fire(CaptureOutcome.DragTrigger drag, OsModel os, Counts counts)
        {
            var pressed = new PressedTrigger(new Trigger.DragTrigger(), drag.Hold);
            if (PressOutputs.For(pressed) is { Output: RemapOutput.Button output })
            {
                counts.DragOutputs++;
                counts.DragHeldKeys += output.HeldKey != KeyCode.None ? 1 : 0;
                os.DragFired(drag.Button, output, handedBack: false);
            }
            else if (PressOutputs.RunsSteps(pressed))
            {
                counts.DragSteps++;
                os.DragFired(drag.Button, null, handedBack: false);
            }
            else
            {
                counts.DragHandBacks++;
                os.DragFired(drag.Button, null, handedBack: true);
            }
        }
    }

    private sealed class Counts
    {
        public int HandedBack;
        public int Joined;
        public int KeysClaimed;
        public int AnchorPresses;
        public int KeyPairs;
        public int EndedElsewhere;
        public int Chords;
        public int DragOutputs;
        public int DragHeldKeys;
        public int DragSteps;
        public int DragHandBacks;
    }
}
