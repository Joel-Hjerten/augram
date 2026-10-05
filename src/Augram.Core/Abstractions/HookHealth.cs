namespace Augram.Core.Abstractions;

/// <summary>
/// One hook lifecycle transition. <paramref name="Generation"/> counts installs since the process
/// started (1 for the first); <paramref name="Detail"/> is the source's own reason text, for the log.
/// </summary>
public sealed record HookHealth(HookHealthKind Kind, int Generation, string? Detail = null);
