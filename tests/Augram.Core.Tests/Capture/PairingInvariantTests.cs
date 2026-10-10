using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>
/// The stuck-button invariant (A19, reference §9) over random but physically plausible input:
/// a stroke-button down that was suppressed is always followed by a suppressed up, a passed-through
/// down by a passed-through up, every stroke-button up lands in Idle, other buttons are never consumed,
/// and every button or wheel event yields exactly one input decision.
/// </summary>
public sealed class PairingInvariantTests
{
    private const MouseButton Stroke = MouseButton.Right;
    private const int Sequences = 1000;
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();

    [Fact]
    public void RandomSequences_KeepEveryDownPairedWithAnEqualUp_AndReturnToIdle()
    {
        var rng = new Random(20261005);
        var suppressedPairs = 0;
        var passedPairs = 0;
        var trailsOpened = 0;

        for (var sequence = 0; sequence < Sequences; sequence++)
        {
            var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: rng.Next(100, 1500)));
            CaptureOutcome? pendingDecision = null;
            var trailOpen = false;

            foreach (var e in Generate(rng))
            {
                var stateBefore = machine.State;
                var outcomes = machine.Handle(e);
                var decision = SingleDecision(outcomes, e);

                switch (e)
                {
                    case CaptureEvent.ButtonDown { Button: Stroke }:
                        Assert.NotNull(decision);
                        pendingDecision = decision;
                        break;
                    case CaptureEvent.ButtonUp { Button: Stroke }:
                        Assert.Equal(pendingDecision ?? CaptureOutcome.PassThrough.Instance, decision);
                        Assert.Equal(CaptureState.Idle, machine.State);
                        if (decision is CaptureOutcome.Suppress)
                        {
                            suppressedPairs++;
                        }
                        else
                        {
                            passedPairs++;
                        }

                        pendingDecision = null;
                        break;
                    case CaptureEvent.ButtonDown or CaptureEvent.ButtonUp:
                        Assert.Equal(CaptureOutcome.PassThrough.Instance, decision);
                        break;
                    case CaptureEvent.Wheel:
                        Assert.NotNull(decision);
                        Assert.Equal(stateBefore is CaptureState.Idle or CaptureState.Cancelled, decision is CaptureOutcome.PassThrough);
                        break;
                    case CaptureEvent.Move:
                        Assert.Equal(stateBefore == CaptureState.Idle, decision is CaptureOutcome.PassThrough);
                        break;
                    case CaptureEvent.Tick:
                        Assert.Null(decision);
                        break;
                }

                foreach (var outcome in outcomes)
                {
                    if (outcome is CaptureOutcome.BeginStroke)
                    {
                        Assert.False(trailOpen, "BeginStroke while a trail is already open");
                        Assert.Equal(CaptureState.Held, stateBefore);
                        trailOpen = true;
                        trailsOpened++;
                    }
                    else if (outcome is CaptureOutcome.EndStroke)
                    {
                        Assert.True(trailOpen, "EndStroke without a trail");
                        trailOpen = false;
                    }
                    else if (outcome is CaptureOutcome.StrokeComplete)
                    {
                        Assert.IsType<CaptureEvent.ButtonUp>(e);
                    }
                }

                Assert.Equal(machine.State == CaptureState.Drawing, trailOpen);
            }

            Assert.Equal(CaptureState.Idle, machine.State);
            Assert.False(trailOpen);
        }

        Assert.True(suppressedPairs > 100, $"only {suppressedPairs} suppressed pairs; the generator is not exercising capture");
        Assert.True(passedPairs > 10, $"only {passedPairs} passed-through pairs");
        Assert.True(trailsOpened > 100, $"only {trailsOpened} strokes began");
    }

    /// <summary>
    /// The same invariant with trigger combinations (Joel, 2026-10-09): random anchor plans per press, so any button may own a
    /// press or join one. Every button's release gets its press's decision, a hand-back's injected down always gets its injected
    /// release, and nothing is owed once every button is up. With another program's releases (plan 0005 decision 10), sometimes
    /// swallowing the real one: the release that still comes keeps its press's decision, a handed-back press is never released
    /// again after the other program released it, and what stays owed is only a release that never came.
    /// </summary>
    [Fact]
    public void RandomSequencesWithAnchorsAndChords_PairEveryButton_AndEveryHandBack()
    {
        var rng = new Random(20261009);
        var joined = 0;
        var handBacks = 0;
        var endedElsewhere = 0;

        for (var sequence = 0; sequence < Sequences; sequence++)
        {
            var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: rng.Next(100, 1500)));
            var pressed = new CaptureOutcome?[Buttons.Length];
            var injected = new bool[Buttons.Length];
            var physicallyDown = HeldButtons.None;
            foreach (var e in Generate(rng, () => RandomPlan(rng), foreignReleases: sequence % 2 == 1))
            {
                var stateBefore = machine.State;
                var outcomes = machine.Handle(e);
                var decision = SingleDecision(outcomes, e);
                switch (e)
                {
                    case CaptureEvent.ButtonDown down:
                        joined += stateBefore is CaptureState.Held or CaptureState.Drawing && decision is CaptureOutcome.Suppress && down.Button != Stroke ? 1 : 0;
                        pressed[(int)down.Button] = decision;
                        physicallyDown |= down.Button.Flag();
                        break;
                    case CaptureEvent.ButtonUp up:
                        Assert.Equal(pressed[(int)up.Button] ?? CaptureOutcome.PassThrough.Instance, decision);
                        pressed[(int)up.Button] = null;
                        physicallyDown &= ~up.Button.Flag();
                        break;
                    case CaptureEvent.ButtonReleasedElsewhere released:
                        Assert.Null(decision);
                        // The other program's release reaches the OS: a handed-back press is released there now.
                        injected[(int)released.Button] = false;
                        endedElsewhere += outcomes.Any(o => o is CaptureOutcome.Cancelled { Reason: CancelReason.ReleasedElsewhere }) ? 1 : 0;
                        break;
                }

                foreach (var outcome in outcomes)
                {
                    if (outcome is CaptureOutcome.HandBack back)
                    {
                        Assert.False(injected[(int)back.Button]);
                        injected[(int)back.Button] = true;
                        handBacks++;
                    }
                    else if (outcome is CaptureOutcome.ReleaseHandedBack release)
                    {
                        Assert.True(injected[(int)release.Button]);
                        injected[(int)release.Button] = false;
                    }
                }
            }

            Assert.Equal(CaptureState.Idle, machine.State);
            // Still down here means the other program swallowed the real release: only those may stay owed.
            Assert.Equal(HeldButtons.None, machine.OwedButtons & ~physicallyDown);
            Assert.All(injected, held => Assert.False(held));
        }

        Assert.True(joined > 100, $"only {joined} buttons joined a press");
        Assert.True(handBacks > 100, $"only {handBacks} hand-backs");
        Assert.True(endedElsewhere > 100, $"only {endedElsewhere} presses ended by a release elsewhere");
    }

    private static AnchorPlan RandomPlan(Random rng)
    {
        var plan = AnchorPlan.None;
        foreach (var button in Buttons)
        {
            plan = rng.Next(3) == 0 ? plan.WithAnchor(button) : plan;
            plan = plan.WithExtras(button, ownerIsStroke: rng.Next(2) == 0, (HeldButtons)(rng.Next(32) << 1));
        }

        return plan;
    }

    /// <summary>Exactly one of Suppress / PassThrough for button and wheel events, none for ticks; returns it.</summary>
    private static CaptureOutcome? SingleDecision(IReadOnlyList<CaptureOutcome> outcomes, CaptureEvent e)
    {
        var decisions = outcomes.Where(o => o is CaptureOutcome.Suppress or CaptureOutcome.PassThrough).ToList();
        Assert.True(decisions.Count <= 1, $"{e} produced {decisions.Count} input decisions");
        if (e is CaptureEvent.ButtonDown or CaptureEvent.ButtonUp or CaptureEvent.Wheel)
        {
            Assert.Single(decisions);
        }

        return decisions.SingleOrDefault();
    }

    /// <summary>
    /// Random events where each button alternates down/up and time never goes backwards; ends with every button released. With
    /// <paramref name="foreignReleases"/>, another program sometimes posts a release of a button that is down, and half the time
    /// swallows the real one, which then never comes.
    /// </summary>
    private static List<CaptureEvent> Generate(Random rng, Func<AnchorPlan>? plan = null, bool foreignReleases = false)
    {
        var events = new List<CaptureEvent>();
        var isDown = new bool[Buttons.Length];
        long t = 0;
        int x = rng.Next(0, 1000), y = rng.Next(0, 1000);
        var count = rng.Next(5, 60);

        for (var i = 0; i < count; i++)
        {
            t += rng.Next(0, 400);
            var button = Buttons[rng.Next(Buttons.Length)];
            if (foreignReleases && isDown[(int)button] && rng.Next(8) == 0)
            {
                events.Add(new CaptureEvent.ButtonReleasedElsewhere(button, x, y, t));
                isDown[(int)button] = rng.Next(2) == 0;
                continue;
            }

            switch (rng.Next(6))
            {
                case 0 when !isDown[(int)button]:
                    isDown[(int)button] = true;
                    events.Add(new CaptureEvent.ButtonDown(button, x, y, t, CaptureAllowed: rng.Next(12) != 0, IgnoreKeyHeld: rng.Next(12) == 0, Plan: plan?.Invoke() ?? AnchorPlan.None));
                    break;
                case 1 when isDown[(int)button]:
                    isDown[(int)button] = false;
                    events.Add(new CaptureEvent.ButtonUp(button, x, y, t));
                    break;
                case 2 or 3:
                    x += rng.Next(-40, 41);
                    y += rng.Next(-40, 41);
                    events.Add(new CaptureEvent.Move(x, y, t));
                    break;
                case 4:
                    events.Add(new CaptureEvent.Wheel(rng.Next(2) == 0 ? WheelDirection.Up : WheelDirection.Down, x, y, t));
                    break;
                default:
                    events.Add(new CaptureEvent.Tick(t));
                    break;
            }
        }

        foreach (var button in Buttons.Where(b => isDown[(int)b]))
        {
            events.Add(new CaptureEvent.ButtonUp(button, x, y, ++t));
        }

        return events;
    }
}
