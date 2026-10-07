namespace Augram.Core.Mapping;

/// <summary>
/// What <see cref="CommandResolver"/> decided for one trigger over one window. <see cref="Reason"/> is
/// one short line that goes straight into the recognition log and the file log ("app override in
/// 'Chrome'", "globals suppressed by 'FF7'", "no command for this gesture").
/// </summary>
public sealed record CommandResolution(ResolutionOutcome Outcome, AppGroup? Group, Command? Command, string Reason)
{
    /// <summary>The ignored app that claimed the window when <see cref="Outcome"/> is <see cref="ResolutionOutcome.Ignored"/>.</summary>
    public IgnoredApp? IgnoredBy { get; init; }

    /// <summary>True when there are steps to run: matched and not an override to nothing.</summary>
    public bool Fires => Outcome == ResolutionOutcome.Matched && Command is { IsOverrideToNothing: false };

    public static CommandResolution Matched(AppGroup group, Command command, string reason)
        => new(ResolutionOutcome.Matched, group, command, reason);

    public static CommandResolution None(string reason)
        => new(ResolutionOutcome.None, null, null, reason);

    public static CommandResolution Ignored(IgnoredApp app)
        => new(ResolutionOutcome.Ignored, null, null, $"ignored app '{app.Name}'") { IgnoredBy = app };
}
