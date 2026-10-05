namespace Augram.Core.Config;

/// <summary>What happens to a completed stroke that matches no active gesture.</summary>
public enum NoMatchBehaviour
{
    /// <summary>Nothing, silently (requirements F3: no ambient learning, no prompt).</summary>
    DoNothing,

    /// <summary>Replay the stroke button click at the release point, as if it had been a plain click.</summary>
    ReplayClick,
}
