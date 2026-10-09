using System.Security.Cryptography;
using System.Text;
using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;

namespace Augram.Core.Mapping;

/// <summary>
/// A named command in an app group (F5a): one trigger, the steps played in order when it fires.
/// An empty step list in an app group is the "override to nothing" in daily use in the reference
/// config (F5: Steam games ignore the global Close). Inactive commands stay in the list and are
/// invisible to the resolver. <paramref name="Note"/> is free text shown read-only; the importer keeps
/// a script-only SP.net action's script there. <paramref name="CategoryId"/> is the section of its group
/// it is sorted into (<see cref="AppGroup.Categories"/>); null is "Uncategorized". <see cref="HoldRemapId"/> puts it under a hold
/// remap instead (F9). Immutable: a change is a new record committed through <see cref="MappingStore"/>.
/// </summary>
public sealed partial record Command(
    CommandId Id,
    string Name,
    Trigger Trigger,
    bool IsActive,
    IReadOnlyList<CommandStep> Steps,
    string? Note = null,
    CategoryId? CategoryId = null)
{
    /// <summary>
    /// Where the command itself takes part (F8 "Use on", Joel 2026-10-07): both by default. A command not used on a platform is
    /// absent there, so its trigger falls through as if it did not exist (an app group's to Global). Its group and its
    /// category can narrow this further; <see cref="AppGroup.IsCommandUsedOn"/> is the whole rule.
    /// </summary>
    public PlatformSet UseOn { get; init; } = PlatformSet.All;

    /// <summary>The command's own value only; ask <see cref="AppGroup.IsCommandUsedOn"/> for whether it is used on a platform.</summary>
    public bool IsUsedOn(HostPlatform platform) => UseOn.Includes(platform);

    /// <summary>
    /// The hold remap of its group the command sits under (F9, plan 0002), as <see cref="CategoryId"/> names a category; null
    /// for an ordinary command. A command under a hold remap has an input (<see cref="Trigger.InputTrigger"/>) or no trigger
    /// yet, and no category; the resolver never fires it. <see cref="HoldRemapRules"/> keeps all of that true.
    /// </summary>
    public HoldRemapId? HoldRemapId { get; init; }

    /// <summary>
    /// The own steps for the platform the command was not authored on (F8, Joel 2026-10-07); null while that platform runs
    /// the converted original. <see cref="Steps"/> stays the original and keeps running where it was authored.
    /// </summary>
    public CommandVersion? OwnVersion { get; init; }

    /// <summary>
    /// The platform the original steps were authored on: the other one than the own version's, else the first step's;
    /// null for a command with no steps and no own version (its first step makes this platform the origin).
    /// </summary>
    public HostPlatform? Origin => OwnVersion is { } own ? Other(own.Platform) : Steps.Count > 0 ? Steps[0].AuthoredOn : null;

    /// <summary>True when the original changed after the own version was made or last checked: its fingerprint moved on.</summary>
    public bool IsOwnVersionStale => OwnVersion is { } own && own.BasedOn != Fingerprint(Steps);

    /// <summary>No steps: in an app group this shadows the global command for the same trigger with nothing.</summary>
    public bool IsOverrideToNothing => Steps.Count == 0;

    /// <summary><see cref="IsOverrideToNothing"/> as <paramref name="platform"/> sees it: its own version when it has one.</summary>
    public bool IsOverrideToNothingOn(HostPlatform platform) => StepsFor(platform).Count == 0;

    /// <summary>The step list <paramref name="platform"/> shows and edits: its own version's, else the original.</summary>
    public IReadOnlyList<CommandStep> StepsFor(HostPlatform platform) => OwnVersion is { } own && own.Platform == platform ? own.Steps : Steps;

    /// <summary>
    /// What runs on <paramref name="platform"/>, step by step: its own version as stored, else the original with each step as it
    /// runs there (itself, converted, or none with the reason).
    /// </summary>
    public IReadOnlyList<PlannedStep> PlanFor(HostPlatform platform)
        => [.. StepsFor(platform).Select(step => new PlannedStep(step, step.ForPlatform(platform)))];

    /// <summary>
    /// The command with <paramref name="steps"/> as <paramref name="platform"/>'s step list. Where the original was authored (or
    /// when there is no original yet) that is the original; elsewhere it is the own version, made on the first edit, and an
    /// edit there counts as having checked it against the current original.
    /// </summary>
    public Command WithStepsFor(HostPlatform platform, IReadOnlyList<CommandStep> steps, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return Origin is not { } origin || origin == platform
            ? this with { Steps = steps }
            : this with { OwnVersion = new CommandVersion(platform, steps, Fingerprint(Steps), now) };
    }

    /// <summary>
    /// The original converted for <paramref name="platform"/>, as the start of an own version: each step as it runs there,
    /// authored there; a step with no guess is kept as it is, so it still says what it needs.
    /// </summary>
    public IReadOnlyList<CommandStep> ConvertedFor(HostPlatform platform)
        => [.. Steps.Select(step => step.ForPlatform(platform) is { Step: { } run } && step.AuthoredOn != platform
            ? new CommandStep(run, platform, step.IsActive)
            : step)];

    /// <summary>Back to running the converted original on the own version's platform.</summary>
    public Command WithoutOwnVersion() => this with { OwnVersion = null };

    /// <summary>The own version marked as checked against the current original, so it is no longer flagged.</summary>
    public Command WithOwnVersionChecked(DateTimeOffset now)
        => OwnVersion is { } own ? this with { OwnVersion = own with { BasedOn = Fingerprint(Steps), ChangedAt = now } } : this;

    /// <summary>
    /// A short, stable fingerprint of a step list: what each step is (type, parameters, where it was authored, active), so
    /// equal lists have equal fingerprints on every machine.
    /// </summary>
    public static string Fingerprint(IReadOnlyList<CommandStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var text = new StringBuilder();
        foreach (var step in steps)
        {
            text.Append(step.Step.StoredKey).Append('|').Append(step.AuthoredOn).Append('|').Append(step.IsActive)
                .Append('|').Append(step.Step.Type.Write(step.Step).ToJsonString()).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())).AsSpan(0, 8));
    }

    private static HostPlatform Other(HostPlatform platform) => platform == HostPlatform.MacOS ? HostPlatform.Windows : HostPlatform.MacOS;
}
