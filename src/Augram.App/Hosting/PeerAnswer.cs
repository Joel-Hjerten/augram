namespace Augram.App.Hosting;

/// <summary>How the running Augram answered a request (<see cref="SingleInstanceGuard.Send"/>).</summary>
/// <param name="Kind">Whether it answered, and how.</param>
/// <param name="Running">Its identity when <see cref="PeerAnswerKind.Answered"/>; null otherwise.</param>
public sealed record PeerAnswer(PeerAnswerKind Kind, InstanceIdentity? Running = null)
{
    public static PeerAnswer NoAnswer { get; } = new(PeerAnswerKind.NoAnswer);

    public static PeerAnswer Older { get; } = new(PeerAnswerKind.Older);
}

/// <summary>The three outcomes of talking to the single-instance pipe.</summary>
public enum PeerAnswerKind
{
    /// <summary>Nobody listened or answered in time (the running one is starting, stopping or stuck).</summary>
    NoAnswer,

    /// <summary>An Augram from before identities (0.1): it shows its window on any connection and says nothing back.</summary>
    Older,

    /// <summary>It answered with its identity (<see cref="PeerAnswer.Running"/>).</summary>
    Answered,
}
