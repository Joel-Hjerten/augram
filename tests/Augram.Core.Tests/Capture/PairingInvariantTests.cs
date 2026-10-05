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

    /// <summary>Random events where each button alternates down/up and time never goes backwards; ends with every button released.</summary>
    private static List<CaptureEvent> Generate(Random rng)
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
            switch (rng.Next(6))
            {
                case 0 when !isDown[(int)button]:
                    isDown[(int)button] = true;
                    events.Add(new CaptureEvent.ButtonDown(button, x, y, t, CaptureAllowed: rng.Next(12) != 0, IgnoreKeyHeld: rng.Next(12) == 0));
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
