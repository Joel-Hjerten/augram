using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// The pairing invariant (A19) for hold remaps over random but physically plausible input, the machine alone: the hold key,
/// input and other buttons, input and other keys with repeats, Shift, both wheel directions and the odd reset, at random
/// times. Every output down gets exactly one up (never an up for something not down), every replayed key down its up, every
/// input event exactly one decision (first), a suppressed press a suppressed release and a passed one a passed one, and once
/// everything is physically up nothing is held and the machine is idle.
/// </summary>
public sealed class HoldRemapPairingTests
{
    private const int Sequences = 2000;
    private const int EventsPerSequence = 60;
    private static readonly MouseButton[] Buttons = [MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.X1];
    private static readonly KeyCode[] Keys = [KeyCode.W, KeyCode.E, KeyCode.Q, KeyCode.A, KeyCode.B, KeyCode.LeftShift];

    [Fact]
    public void RandomSequences_PairEveryOutputAndEveryDecision_AndEndIdle()
    {
        var rng = new Random(20261010);
        var entry = Blender.Entry();
        var outputsPressed = 0;
        var replays = 0;
        var taps = 0;

        for (var sequence = 0; sequence < Sequences; sequence++)
        {
            var run = new Run(entry);
            for (var i = 0; i < EventsPerSequence; i++)
            {
                run.Step(rng);
            }

            run.ReleaseEverything();

            Assert.All(run.Held, pair => Assert.Equal(0, pair.Value));
            Assert.Empty(run.Replayed);
            Assert.Equal(HoldRemapState.Idle, run.Machine.State);
            outputsPressed += run.Pressed;
            replays += run.Replays;
            taps += run.Taps;
        }

        // The generator reaches every branch that matters, not just idle input.
        Assert.True(outputsPressed > 10_000, $"only {outputsPressed} outputs pressed");
        Assert.True(replays > 200, $"only {replays} rollovers");
        Assert.True(taps > 500, $"only {taps} taps");
    }

    /// <summary>One sequence: the physical state, what the outputs hold, and the decisions owed.</summary>
    private sealed class Run(HoldRemapEntry entry)
    {
        private readonly HashSet<MouseButton> _buttonsDown = [];
        private readonly HashSet<KeyCode> _keysDown = [];
        private readonly Dictionary<object, string> _decisions = [];
        private bool _spaceDown;
        private long _now;

        public HoldRemapMachine Machine { get; } = new();

        public Dictionary<RemapOutput, int> Held { get; } = [];

        public HashSet<KeyCode> Replayed { get; } = [];

        public int Pressed { get; private set; }

        public int Replays { get; private set; }

        public int Taps { get; private set; }

        public void Step(Random rng)
        {
            _now += rng.Next(0, 4) == 0 ? rng.Next(150, 400) : rng.Next(0, 120);
            var roll = rng.Next(100);
            if (roll < 20)
            {
                Space(rng);
            }
            else if (roll < 50)
            {
                Button(Buttons[rng.Next(Buttons.Length)]);
            }
            else if (roll < 58)
            {
                Feed(new HoldRemapEvent.Wheel(rng.Next(2) == 0 ? WheelDirection.Up : WheelDirection.Down, 5, 5, _now), owner: null);
            }
            else if (roll < 99)
            {
                Key(Keys[rng.Next(Keys.Length)], rng);
            }
            else
            {
                Feed(new HoldRemapEvent.Reset(_now), owner: null);
                _decisions.Clear();
            }
        }

        public void ReleaseEverything()
        {
            foreach (var button in _buttonsDown.ToArray())
            {
                Button(button);
            }

            foreach (var key in _keysDown.ToArray())
            {
                Feed(new HoldRemapEvent.Key(key, KeyPhase.Up, _now), key, closes: true);
                _keysDown.Remove(key);
            }

            if (_spaceDown)
            {
                _now += 10;
                Feed(new HoldRemapEvent.HoldUp(KeyCode.Space, _now), KeyCode.Space, closes: true);
                _spaceDown = false;
            }
        }

        private void Space(Random rng)
        {
            if (!_spaceDown)
            {
                _spaceDown = true;
                Feed(new HoldRemapEvent.HoldDown(entry, _now), KeyCode.Space);
            }
            else if (rng.Next(3) == 0)
            {
                Feed(new HoldRemapEvent.Key(KeyCode.Space, KeyPhase.Repeat, _now), KeyCode.Space, continues: true);
            }
            else
            {
                _spaceDown = false;
                Feed(new HoldRemapEvent.HoldUp(KeyCode.Space, _now), KeyCode.Space, closes: true);
            }
        }

        private void Button(MouseButton button)
        {
            var down = _buttonsDown.Add(button);
            if (!down)
            {
                _buttonsDown.Remove(button);
            }

            Feed(new HoldRemapEvent.Button(button, down, 5, 5, _now), button, closes: !down);
        }

        private void Key(KeyCode key, Random rng)
        {
            if (_keysDown.Add(key))
            {
                Feed(new HoldRemapEvent.Key(key, KeyPhase.Down, _now), key);
            }
            else if (rng.Next(3) == 0)
            {
                Feed(new HoldRemapEvent.Key(key, KeyPhase.Repeat, _now), key, continues: true);
            }
            else
            {
                _keysDown.Remove(key);
                Feed(new HoldRemapEvent.Key(key, KeyPhase.Up, _now), key, closes: true);
            }
        }

        /// <summary>Feeds one event and checks its outcomes; <paramref name="owner"/> is the button or key whose press the decision pairs with.</summary>
        private void Feed(HoldRemapEvent e, object? owner, bool continues = false, bool closes = false)
        {
            var outcomes = Machine.Handle(e);
            if (e is HoldRemapEvent.Reset)
            {
                Assert.DoesNotContain(outcomes, outcome => outcome is HoldRemapOutcome.Suppress or HoldRemapOutcome.PassThrough);
            }
            else
            {
                Assert.True(outcomes.Count > 0 && outcomes[0] is HoldRemapOutcome.Suppress or HoldRemapOutcome.PassThrough, $"{e}: no decision first");
                Assert.Single(outcomes, outcome => outcome is HoldRemapOutcome.Suppress or HoldRemapOutcome.PassThrough);
                if (owner is not null)
                {
                    CheckDecision(owner, Decision(outcomes[0]), continues || closes, closes);
                }
            }

            foreach (var outcome in outcomes)
            {
                Apply(e, outcome);
            }
        }

        /// <summary>A press's repeats and release get the decision its down got, unless a reset came between (the machine forgot the press then).</summary>
        private void CheckDecision(object owner, string decision, bool follows, bool closes)
        {
            if (!follows)
            {
                _decisions[owner] = decision;
                return;
            }

            if (_decisions.TryGetValue(owner, out var atPress))
            {
                Assert.Equal(atPress, decision);
            }

            if (closes)
            {
                _decisions.Remove(owner);
            }
        }

        private void Apply(HoldRemapEvent e, HoldRemapOutcome outcome)
        {
            switch (outcome)
            {
                case HoldRemapOutcome.PressOutput press:
                    Held[press.Output] = Held.GetValueOrDefault(press.Output) + 1;
                    Pressed++;
                    break;
                case HoldRemapOutcome.RepeatOutput repeat:
                    Assert.True(Held.GetValueOrDefault(repeat.Output) > 0, $"{e}: repeat of {repeat.Output}, which is not down");
                    break;
                case HoldRemapOutcome.ReleaseOutput release:
                    Assert.True(Held.GetValueOrDefault(release.Output) > 0, $"{e}: release of {release.Output}, which is not down");
                    Held[release.Output]--;
                    break;
                case HoldRemapOutcome.ReplayKey { Phase: KeyPhase.Down } replay:
                    Assert.True(Replayed.Add(replay.Key), $"{e}: {replay.Key} replayed down twice");
                    Replays++;
                    break;
                case HoldRemapOutcome.ReplayKey { Phase: KeyPhase.Repeat } replay:
                    Assert.Contains(replay.Key, Replayed);
                    break;
                case HoldRemapOutcome.ReplayKey replay:
                    Assert.True(Replayed.Remove(replay.Key), $"{e}: {replay.Key} replayed up without its down");
                    break;
                case HoldRemapOutcome.TapHoldKey:
                    Taps++;
                    break;
            }
        }

        private static string Decision(HoldRemapOutcome outcome) => outcome is HoldRemapOutcome.Suppress ? "suppress" : "pass";
    }
}
