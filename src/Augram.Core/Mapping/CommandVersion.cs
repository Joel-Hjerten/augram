using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// A command's own steps for the platform it was not authored on (F8, Joel 2026-10-07): made from the converted original
/// the first time the steps are edited there, and from then on what runs there. <see cref="BasedOn"/> is the
/// <see cref="Command.Fingerprint"/> of the original it was made from, or last checked against, so a later change to the
/// original shows ("the Windows original changed since this version was made"); <see cref="ChangedAt"/> is when it was
/// last edited, for display only (two machines' clocks are never compared). An empty step list means "does nothing on
/// this platform".
/// </summary>
public sealed record CommandVersion(HostPlatform Platform, IReadOnlyList<CommandStep> Steps, string BasedOn, DateTimeOffset ChangedAt);
