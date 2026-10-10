using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// A19 with hold remaps (F9, plan 0002 step 3). The real <see cref="InputGate"/> (hold remap shadow first, then the gesture
/// shadow and the key record) decides on random sequences of hold keys (Space and D, with repeats), input and other buttons,
/// the stroke button (an input of the hold or not), input, other and modifier keys with repeats, and both wheel directions,
/// while the foreground's plan (Blender's or none), the pointer's answer (ignore bits, anchor plans), the tray toggle, the
/// hotkey capture and the stroke button change between events. This test plays the worker, lagging behind the hook by a
/// random number of messages: it runs a <see cref="HoldRemapMachine"/> and a <see cref="CaptureStateMachine"/> on what the
/// gate posted (the foreground changing mid-hold too: to none or another group ends a hold with nothing owed, the same group's
/// plan republished does not), checks every hold decision equals the machine's (and every capture decision the worker would warn about),
/// and applies what the worker injects to a model of the OS. Proven, per sequence: a release gets its press's decision, the
/// OS never gets an up for something it does not hold, a key the hook lets through never overtakes a replay still queued,
/// and once everything is physically up the OS holds nothing, both machines are idle and no replay is pending.
/// </summary>
public sealed class HoldPairingTests
{
    private const int Sequences = 6000;
    private static readonly MouseButton[] Buttons = [MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.X1];
    private static readonly KeyCode[] Letters = [KeyCode.W, KeyCode.E, KeyCode.Q, KeyCode.A, KeyCode.B];
    private static readonly KeyCode[] Modifiers = [KeyCode.RightShift, KeyCode.RightControl];
    private static readonly AppGroup BlenderGroup = BlenderHold.Document().Groups[1];
    private static readonly HoldRemapPlan Blender = HoldRemapPlan.ForGroup(BlenderGroup, HostPlatform.Windows);

    // The same group's plan built again (a mapping edit republishes it): a hold must not end for it.
    private static readonly HoldRemapPlan BlenderAgain = HoldRemapPlan.ForGroup(BlenderGroup, HostPlatform.Windows);

    // Another app group with the same hold remaps: switching to it mid-hold ends the hold as switching to none does.
    private static readonly HoldRemapPlan Other = HoldRemapPlan.ForGroup(BlenderGroup with { Id = GroupId.New(), Name = "Other" }, HostPlatform.Windows);
    private static readonly HoldRemapPlan[] Plans = [HoldRemapPlan.Empty, Blender, BlenderAgain, Other];

    [Fact]
    public void RandomSequences_HookEqualsMachine_PairEverything_AndLeaveTheOsHoldingNothing()
    {
        Assert.Equal(2, Blender.Entries.Count);
        Assert.NotSame(Blender, BlenderAgain);
        Assert.Equal(Blender.GroupId, BlenderAgain.GroupId);
        var rng = new Random(20261010);
        var counts = new Counts();

        for (var sequence = 0; sequence < Sequences; sequence++)
        {
            var run = new Run(sequence, Buttons[rng.Next(Buttons.Length)], counts);
            run.Play(rng, Generate(rng));
        }

        // The generator reaches every branch that matters, not just idle input (6,000 sequences give about 4,000 holds).
        Assert.True(counts.Holds > 2500, $"only {counts.Holds} holds");
        Assert.True(counts.Taps > 900, $"only {counts.Taps} taps");
        Assert.True(counts.Outputs > 2500, $"only {counts.Outputs} outputs pressed");
        Assert.True(counts.Rollovers > 500, $"only {counts.Rollovers} rollovers");
        Assert.True(counts.Ordered > 80, $"only {counts.Ordered} keys kept in order behind a replay");
        Assert.True(counts.StrokeInputs > 500, $"only {counts.StrokeInputs} stroke-button presses taken by a hold");
        Assert.True(counts.Strokes > 1500, $"only {counts.Strokes} presses captured as gestures");
        Assert.True(counts.KeyPairs > 30_000, $"only {counts.KeyPairs} key presses released");
        Assert.True(counts.FocusEnds > 700, $"only {counts.FocusEnds} holds ended by focus moving");
        Assert.True(counts.FocusKept > 600, $"only {counts.FocusKept} republished plans mid-hold that left the hold alone");
    }

    /// <summary>Plausible input within 2 s (key records never age out): hold keys, buttons and keys go down and up with repeats, mouse events carry the modifiers held; everything released at the end.</summary>
    private static List<RawInput> Generate(Random rng)
    {
        var events = new List<RawInput>();
        var buttonDown = new HashSet<MouseButton>();
        var keyDown = new HashSet<KeyCode>();
        long t = 0;
        int x = rng.Next(0, 1000), y = rng.Next(0, 1000);
        var count = rng.Next(5, 48);

        KeyModifiers Held() => (keyDown.Contains(KeyCode.RightShift) ? KeyModifiers.Shift : KeyModifiers.None)
            | (keyDown.Contains(KeyCode.RightControl) ? KeyModifiers.Control : KeyModifiers.None);

        void Key(KeyCode key)
        {
            if (keyDown.Add(key) || rng.Next(3) == 0)
            {
                events.Add(RawInput.KeyDown(key, t, Held()));
                return;
            }

            keyDown.Remove(key);
            events.Add(RawInput.KeyUp(key, t, Held()));
        }

        for (var i = 0; i < count; i++)
        {
            t += rng.Next(0, 40);
            var roll = rng.Next(100);
            if (roll < 18)
            {
                Key(KeyCode.Space);
            }
            else if (roll < 22)
            {
                Key(KeyCode.D);
            }
            else if (roll < 46)
            {
                var button = Buttons[rng.Next(Buttons.Length)];
                if (buttonDown.Add(button))
                {
                    events.Add(RawInput.ButtonDown(button, x, y, t, Held()));
                }
                else
                {
                    buttonDown.Remove(button);
                    events.Add(RawInput.ButtonUp(button, x, y, t, Held()));
                }
            }
            else if (roll < 53)
            {
                events.Add(RawInput.WheelTick(rng.Next(2) == 0 ? WheelDirection.Up : WheelDirection.Down, x, y, t, Held()));
            }
            else if (roll < 80)
            {
                Key(Letters[rng.Next(Letters.Length)]);
            }
            else if (roll < 88)
            {
                Key(Modifiers[rng.Next(Modifiers.Length)]);
            }
            else if (roll < 96)
            {
                x += rng.Next(-40, 41);
                y += rng.Next(-40, 41);
                events.Add(RawInput.Move(x, y, t));
            }
        }

        foreach (var button in buttonDown)
        {
            events.Add(RawInput.ButtonUp(button, x, y, ++t));
        }

        foreach (var key in keyDown)
        {
            events.Add(RawInput.KeyUp(key, ++t));
        }

        return events;
    }

    /// <summary>One sequence: the gate, the worker's two machines, the OS model and the decisions to pair.</summary>
    private sealed class Run
    {
        private readonly int _sequence;
        private readonly Counts _counts;
        private readonly Channel<WorkerMessage> _queue = Channel.CreateUnbounded<WorkerMessage>();
        private readonly InputGate _gate;
        private readonly CaptureStateMachine _capture;
        private readonly HoldRemapMachine _hold = new();
        private readonly Os _os;
        private readonly HashSet<KeyCode> _orderedDown = [];
        private readonly Dictionary<object, bool> _pressDecisions = [];

        public Run(int sequence, MouseButton strokeButton, Counts counts)
        {
            _sequence = sequence;
            _counts = counts;
            _gate = new InputGate(_queue.Writer, NullEventLog.Instance, strokeButton, KeyModifiers.Control, enabled: true);
            _capture = new CaptureStateMachine(strokeButton, new CaptureThresholds(CancelDelayMs: 400));
            _os = new Os(sequence);
        }

        public void Play(Random rng, List<RawInput> events)
        {
            _gate.PublishForeground(rng.Next(4) == 0 ? HoldRemapPlan.Empty : Blender);
            var holdingBefore = false;
            foreach (var raw in events)
            {
                var planBefore = _gate.ForegroundPlan;
                Vary(rng);
                if (holdingBefore && _gate.Hold.Holding && !ReferenceEquals(planBefore, _gate.ForegroundPlan) && _gate.Hold.Group == _gate.ForegroundPlan.GroupId)
                {
                    Assert.False(_gate.Hold.FocusMovedFrom(_gate.ForegroundPlan.GroupId), $"sequence {_sequence}: the same group's plan republished would end the hold");
                    _counts.FocusKept++;
                }

                var pendingBefore = _gate.Hold.PendingReplays;
                var fresh = raw.Kind == RawInputKind.KeyDown && !_os.HoldsPhysically(raw.Key);
                var suppressed = _gate.Handle(in raw);
                _os.Physical(raw, suppressed);
                if (fresh && !suppressed)
                {
                    Assert.True(pendingBefore == 0, $"sequence {_sequence}: {raw.Key} went to the OS ahead of {pendingBefore} queued replays");
                }

                Pair(raw, suppressed);
                Drain(rng.Next(5) < 2 ? int.MaxValue : rng.Next(0, 4));
                holdingBefore = _gate.Hold.Holding;
            }

            Drain(int.MaxValue);
            _os.AssertNothingHeld();
            Assert.Equal(HoldRemapState.Idle, _hold.State);
            Assert.True(_gate.Hold.Idle, $"sequence {_sequence}: the hook's hold record is not idle");
            Assert.Equal(0, _gate.Hold.PendingReplays);
            Assert.Equal(CaptureState.Idle, _capture.State);
        }

        /// <summary>Between events: the app in front, the window under the pointer, the tray toggle, the hotkey capture, the stroke button (in order with the input, as the worker applies it).</summary>
        private void Vary(Random rng)
        {
            if (rng.Next(_gate.Hold.Holding ? 6 : 20) == 0)
            {
                // Mid-hold more often: to none, to another group, or the same group's plan republished.
                _gate.PublishForeground(Plans[rng.Next(Plans.Length)]);
            }

            if (rng.Next(12) == 0)
            {
                var plan = rng.Next(3) == 0 ? AnchorPlan.None.WithAnchor(Buttons[rng.Next(Buttons.Length)]) : AnchorPlan.None;
                _gate.PublishPointer(rng.Next(6) == 0 ? rng.Next(1, 4) : 0, plan);
            }

            if (rng.Next(30) == 0)
            {
                _gate.Enabled = !_gate.Enabled;
            }

            if (rng.Next(60) == 0)
            {
                _gate.CaptureKeys(!_gate.KeysCaptured);
            }

            if (rng.Next(50) == 0)
            {
                // Applied with the worker caught up: a press decided between the setter and the worker applying it is a race of
                // its own (one press handled with the old button), which ChordPairingTests leaves out the same way.
                Drain(int.MaxValue);
                var button = Buttons[rng.Next(Buttons.Length)];
                _capture.StrokeButton = button;
                _gate.PublishStrokeButton(button);
                _gate.PublishState(_capture.State);
            }
        }

        /// <summary>The worker's half, up to <paramref name="max"/> messages: run the machines, check the hook agreed, apply what the worker injects.</summary>
        private void Drain(int max)
        {
            for (var i = 0; i < max && _queue.Reader.TryRead(out var message); i++)
            {
                switch (message.Kind)
                {
                    case WorkerMessage.MessageKind.Input:
                        OnCapture(message.Event!, message.HookSuppressed);
                        break;
                    case WorkerMessage.MessageKind.Hold:
                        OnHold((HoldRemapEvent)message.Payload!, message.HookSuppressed, message.Ordered);
                        if (message.Ordered)
                        {
                            _gate.HoldReplayDone();
                        }

                        break;
                    case WorkerMessage.MessageKind.HoldReplay:
                        var key = (HoldRemapEvent.Key)message.Payload!;
                        _counts.Ordered += key.Phase == KeyPhase.Down ? 1 : 0;
                        if (key.Phase != KeyPhase.Up)
                        {
                            _orderedDown.Add(key.KeyCode);
                            _os.KeyDown(key.KeyCode);
                        }
                        else if (_orderedDown.Remove(key.KeyCode))
                        {
                            _os.KeyUp(key.KeyCode, "an in-order replay");
                        }

                        _gate.HoldReplayDone();
                        break;
                }
            }
        }

        private void OnCapture(CaptureEvent e, bool hookSuppressed)
        {
            var activeBefore = _capture.ActiveButton;
            var outcomes = _capture.Handle(e);
            _gate.PublishState(_capture.State);
            if (e is CaptureEvent.ButtonDown or CaptureEvent.ButtonUp or CaptureEvent.Wheel)
            {
                // The worker's rule: a wheel tick or a button joining a press may race the published state; the anchor's own press and release may not.
                var raced = e is CaptureEvent.Wheel || (e is CaptureEvent.ButtonDown down && down.Button != activeBefore) || (e is CaptureEvent.ButtonUp up && up.Button != activeBefore);
                var machineSays = outcomes.Any(outcome => outcome is CaptureOutcome.Suppress);
                Assert.True(raced || machineSays == hookSuppressed, $"sequence {_sequence}: {e}: hook {hookSuppressed}, machine {machineSays}");
                _counts.Strokes += e is CaptureEvent.ButtonDown && machineSays ? 1 : 0;
            }

            foreach (var outcome in outcomes)
            {
                switch (outcome)
                {
                    case CaptureOutcome.HandBack back:
                        _os.ButtonDown(back.Button, "a hand-back");
                        break;
                    case CaptureOutcome.ReleaseHandedBack release:
                        _os.ButtonUp(release.Button, "a hand-back release");
                        break;
                }
            }
        }

        private void OnHold(HoldRemapEvent e, bool hookSuppressed, bool ordered)
        {
            var before = _hold.State;
            var outcomes = _hold.Handle(e);
            // A focus move carries no input decision: the hook ended its hold, and the machine must end its own.
            var machineSays = e is HoldRemapEvent.FocusMoved ? outcomes.Any(outcome => outcome is HoldRemapOutcome.HoldEnded) : outcomes[0] is HoldRemapOutcome.Suppress;
            Assert.True(machineSays == hookSuppressed, $"sequence {_sequence}: {e}: hook {hookSuppressed}, machine {machineSays} (machine was {before})");
            _counts.FocusEnds += e is HoldRemapEvent.FocusMoved ? 1 : 0;
            // The hook keeps later keys behind exactly the messages whose processing injects a key (a tap, a replay).
            var injectsKey = outcomes.Any(outcome => outcome is HoldRemapOutcome.TapHoldKey or HoldRemapOutcome.ReplayKey);
            Assert.True(injectsKey == ordered, $"sequence {_sequence}: {e}: injects a key {injectsKey}, counted as a pending replay {ordered}");
            _counts.Holds += e is HoldRemapEvent.HoldDown && before is HoldRemapState.Idle or HoldRemapState.Following ? 1 : 0;
            _counts.StrokeInputs += e is HoldRemapEvent.Button { IsDown: true } pressed && pressed.MouseButton == _capture.StrokeButton && machineSays ? 1 : 0;
            foreach (var outcome in outcomes)
            {
                switch (outcome)
                {
                    case HoldRemapOutcome.TapHoldKey tap:
                        _counts.Taps++;
                        _os.KeyDown(tap.Key);
                        _os.KeyUp(tap.Key, "a tap");
                        break;
                    case HoldRemapOutcome.PressOutput { Output: RemapOutput.Button button }:
                        _counts.Outputs++;
                        _os.ButtonDown(button.MouseButton, "an output");
                        break;
                    case HoldRemapOutcome.PressOutput { Output: RemapOutput.Key key }:
                        _counts.Outputs++;
                        Modifier(key.Modifiers, down: true);
                        _os.KeyDown(key.KeyCode);
                        break;
                    case HoldRemapOutcome.RepeatOutput { Output: RemapOutput.Key key }:
                        Assert.True(_os.Holds(key.KeyCode), $"sequence {_sequence}: a repeat of {key.KeyCode}, which the OS does not hold");
                        break;
                    case HoldRemapOutcome.ReleaseOutput { Output: RemapOutput.Button button }:
                        _os.ButtonUp(button.MouseButton, "an output release");
                        break;
                    case HoldRemapOutcome.ReleaseOutput { Output: RemapOutput.Key key }:
                        _os.KeyUp(key.KeyCode, "an output release");
                        Modifier(key.Modifiers, down: false);
                        break;
                    case HoldRemapOutcome.ReplayKey replay when replay.Phase == KeyPhase.Up:
                        _os.KeyUp(replay.Key, "a replayed release");
                        break;
                    case HoldRemapOutcome.ReplayKey replay:
                        _counts.Rollovers += replay.Phase == KeyPhase.Down ? 1 : 0;
                        _os.KeyDown(replay.Key);
                        break;
                }
            }
        }

        private void Modifier(KeyModifiers modifiers, bool down)
        {
            if ((modifiers & KeyModifiers.Control) == 0)
            {
                return;
            }

            if (down)
            {
                _os.KeyDown(KeyCode.LeftControl);
            }
            else
            {
                _os.KeyUp(KeyCode.LeftControl, "an output's modifier");
            }
        }

        /// <summary>A19 per button and per key: a release gets its press's decision; key repeats follow their press.</summary>
        private void Pair(RawInput raw, bool suppressed)
        {
            switch (raw.Kind)
            {
                case RawInputKind.ButtonDown:
                    _pressDecisions[raw.Button] = suppressed;
                    break;
                case RawInputKind.ButtonUp when _pressDecisions.Remove(raw.Button, out var pressed):
                    Assert.True(pressed == suppressed, $"sequence {_sequence}: {raw.Button} pressed {(pressed ? "consumed" : "passed")}, released {(suppressed ? "consumed" : "passed")}");
                    break;
                case RawInputKind.KeyDown when _pressDecisions.TryGetValue(raw.Key, out var first):
                    Assert.True(first == suppressed, $"sequence {_sequence}: a repeat of {raw.Key} did not follow its press");
                    break;
                case RawInputKind.KeyDown:
                    _pressDecisions[raw.Key] = suppressed;
                    break;
                case RawInputKind.KeyUp when _pressDecisions.Remove(raw.Key, out var pressedKey):
                    Assert.True(pressedKey == suppressed, $"sequence {_sequence}: {raw.Key} pressed {(pressedKey ? "consumed" : "passed")}, released {(suppressed ? "consumed" : "passed")}");
                    _counts.KeyPairs++;
                    break;
            }
        }
    }

    /// <summary>
    /// What the OS believes is held: physical events the hook passed, plus what the worker injects. An up for something not held
    /// is a stuck-key bug's twin. Buttons are counted: a physical Middle held through a hold whose output is Middle gets a second
    /// down (as AutoHotkey's script would send it); the OS ends released as long as every down has its up, which is what counts.
    /// </summary>
    private sealed class Os(int sequence)
    {
        private readonly Dictionary<MouseButton, int> _buttons = [];
        private readonly HashSet<KeyCode> _keys = [];
        private readonly HashSet<KeyCode> _physical = [];

        public bool Holds(KeyCode key) => _keys.Contains(key);

        /// <summary>The hook has seen this key go down and not up (whatever it decided): a down now is a repeat.</summary>
        public bool HoldsPhysically(KeyCode key) => _physical.Contains(key);

        public void Physical(RawInput raw, bool suppressed)
        {
            if (raw.Kind == RawInputKind.KeyDown)
            {
                _physical.Add(raw.Key);
            }
            else if (raw.Kind == RawInputKind.KeyUp)
            {
                _physical.Remove(raw.Key);
            }

            if (suppressed)
            {
                return;
            }

            switch (raw.Kind)
            {
                case RawInputKind.ButtonDown:
                    ButtonDown(raw.Button, "a passed press");
                    break;
                case RawInputKind.ButtonUp:
                    ButtonUp(raw.Button, "a passed release");
                    break;
                case RawInputKind.KeyDown:
                    KeyDown(raw.Key);
                    break;
                case RawInputKind.KeyUp:
                    KeyUp(raw.Key, "a passed release");
                    break;
            }
        }

        public void ButtonDown(MouseButton button, string what) => _buttons[button] = _buttons.GetValueOrDefault(button) + 1;

        public void ButtonUp(MouseButton button, string what)
        {
            Assert.True(_buttons.GetValueOrDefault(button) > 0, $"sequence {sequence}: {what} of {button}, which the OS does not hold");
            _buttons[button]--;
        }

        public void KeyDown(KeyCode key) => _keys.Add(key);

        public void KeyUp(KeyCode key, string what)
            => Assert.True(_keys.Remove(key), $"sequence {sequence}: {what} of {key}, which the OS does not hold");

        public void AssertNothingHeld()
        {
            Assert.True(_buttons.Values.All(count => count == 0), $"sequence {sequence}: the OS still holds {string.Join(", ", _buttons.Where(pair => pair.Value != 0))}");
            Assert.True(_keys.Count == 0, $"sequence {sequence}: the OS still holds {string.Join(", ", _keys)}");
        }
    }

    private sealed class Counts
    {
        public int Holds;
        public int Taps;
        public int Outputs;
        public int Rollovers;
        public int Ordered;
        public int StrokeInputs;
        public int Strokes;
        public int KeyPairs;
        public int FocusEnds;
        public int FocusKept;
    }
}
