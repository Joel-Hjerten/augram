using Augram.Core.Capture;

namespace Augram.Engine.Input;

/// <summary>What the hook does with a simulated button event (<see cref="ForeignInputRules.Button"/>).</summary>
internal enum SimulatedButtonVerdict
{
    /// <summary>Augram's own (claimed), or foreign input the policy drops: nothing reaches the handler.</summary>
    Drop,

    /// <summary>Another process's, taken as real: an ordinary <c>ButtonDown</c> or <c>ButtonUp</c>, suppressible like any other.</summary>
    Take,

    /// <summary>Another process's release under <see cref="ForeignInput.Drop"/>: <c>RawInputKind.ButtonReleasedElsewhere</c>, never suppressed.</summary>
    ReleasedElsewhere,
}

/// <summary>
/// The per-event decision of <see cref="SharpHookInputSource"/> for simulated mouse events, pulled out so it can be tested without a
/// desktop: the event's kind, the <see cref="ForeignInput"/> policy and the ledger's claim decide whether it is taken as real, dropped,
/// or (a release under <see cref="ForeignInput.Drop"/>) taken as released elsewhere. Hook thread: at most one <c>TryClaim</c> (one short
/// lock) per event, no allocation. Without a ledger every simulated event is dropped, whatever the policy: nothing could tell Augram's
/// own re-posted drags from the pen's moves, and taking them would swallow and re-post them without end.
/// </summary>
internal static class ForeignInputRules
{
    /// <summary>A simulated press (<paramref name="pressed"/>) or release of <paramref name="button"/>.</summary>
    public static SimulatedButtonVerdict Button(ForeignInput policy, OwnInjections? own, MouseButton button, bool pressed)
    {
        if (own is null)
        {
            return SimulatedButtonVerdict.Drop;
        }

        if (pressed)
        {
            // Under Drop a press is never claimed: the simulator still announces it, and the announcement expires unclaimed.
            return policy == ForeignInput.Take && !own.TryClaim(OwnInjections.PressSlotOf(button)) ? SimulatedButtonVerdict.Take : SimulatedButtonVerdict.Drop;
        }

        if (own.TryClaim(OwnInjections.ReleaseSlotOf(button)))
        {
            return SimulatedButtonVerdict.Drop;
        }

        return policy == ForeignInput.Take ? SimulatedButtonVerdict.Take : SimulatedButtonVerdict.ReleasedElsewhere;
    }

    /// <summary>True when a simulated move or drag reaches the handler as an ordinary move: foreign input is taken and the ledger does not claim it.</summary>
    public static bool TakesMove(ForeignInput policy, OwnInjections? own)
        => policy == ForeignInput.Take && own is not null && !own.TryClaim(InjectionSlot.Move);
}
