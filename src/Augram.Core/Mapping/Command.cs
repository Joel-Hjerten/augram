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
/// it is sorted into (<see cref="AppGroup.Categories"/>); null is none, which only an app group's command keeps (on Global the
/// rules put it in Uncategorized, <see cref="CategoryRules.Normalised"/>). <see cref="HoldRemapId"/> puts it under a hold
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
    /// The apps the command is not used over (Joel, 2026-10-10, plan 0004: "Not in"): entries of Ignored › Per command
    /// (<see cref="IgnoredApp"/> with <see cref="IgnoreScope.PerCommand"/>). Over a window one of them claims, the command is as if
    /// it did not exist, so it fires nothing and holds no button back there (an app group's command falls through to Global's).
    /// It names only Per command entries that exist, once each, sorted by id, and is empty under a hold remap
    /// (<see cref="MappingRules.ValidDocument"/>).
    /// </summary>
    public IReadOnlyList<GroupId> NotIn { get; init; } = [];

    /// <summary>True when <see cref="NotIn"/> names one of <paramref name="under"/>, the Per command entries claiming a window (<see cref="IgnoreList.PerCommandUnder"/>).</summary>
    public bool IsNotIn(IReadOnlyList<GroupId> under)
    {
        ArgumentNullException.ThrowIfNull(under);
        return NotIn.Count > 0 && under.Count > 0 && NotIn.Any(under.Contains);
    }

    /// <summary>
    /// The excluded apps the command still works over (Joel, 2026-10-10, plan 0005 decision 7: "Also in"): entries of
    /// Exclusions › Global without the disable-while-focused mode. Over a window one of them claims, the command is the only kind
    /// that applies there: its anchors are held back and it fires, while the stroke button stays the app's. Only a command whose
    /// trigger holds no stroke button on some platform keeps it (a button trigger, Right + wheel), and none under a hold remap;
    /// it names only such entries that exist, once each, sorted by id (<see cref="MappingRules.ValidDocument"/>).
    /// </summary>
    public IReadOnlyList<GroupId> AlsoIn { get; init; } = [];

    /// <summary>True when <see cref="AlsoIn"/> names <paramref name="excluded"/>, the Exclusions › Global entry claiming a window.</summary>
    public bool IsAlsoIn(GroupId excluded) => AlsoIn.Count > 0 && AlsoIn.Contains(excluded);

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
    /// null for a command with no steps and no own version (its first step makes this platform the origin). The side rules
    /// here are <see cref="PlatformSides"/>'s, shared with the Exclusions › While holding inputs (plan 0012).
    /// </summary>
    public HostPlatform? Origin => PlatformSides.Origin(Steps, OwnVersion?.Platform);

    /// <summary>True when the original changed after the own version was made or last checked: its fingerprint moved on.</summary>
    public bool IsOwnVersionStale => OwnVersion is { } own && own.BasedOn != Fingerprint(Steps);

    /// <summary>No steps: in an app group this shadows the global command for the same trigger with nothing.</summary>
    public bool IsOverrideToNothing => Steps.Count == 0;

    /// <summary><see cref="IsOverrideToNothing"/> as <paramref name="platform"/> sees it: its own version when it has one.</summary>
    public bool IsOverrideToNothingOn(HostPlatform platform) => StepsFor(platform).Count == 0;

    /// <summary>The step list <paramref name="platform"/> shows and edits: its own version's, else the original.</summary>
    public IReadOnlyList<CommandStep> StepsFor(HostPlatform platform) => PlatformSides.For(platform, Steps, OwnVersion?.Platform, OwnVersion?.Steps);

    /// <summary>True when <paramref name="platform"/> runs the original converted: it is not the origin and has no own version (<see cref="PlatformSides.IsInferred"/>).</summary>
    public bool IsInferredOn(HostPlatform platform) => PlatformSides.IsInferred(Origin, OwnVersion?.Platform, platform);

    /// <summary>The step list an edit on <paramref name="platform"/> starts from: the original converted where it is inferred, else <see cref="StepsFor"/>.</summary>
    public IReadOnlyList<CommandStep> EditableStepsFor(HostPlatform platform) => IsInferredOn(platform) ? ConvertedFor(platform) : StepsFor(platform);

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
        return PlatformSides.EditsOriginal(Origin, platform)
            ? this with { Steps = steps }
            : this with { OwnVersion = new CommandVersion(platform, steps, Fingerprint(Steps), now) { Trigger = OwnVersion?.Trigger } };
    }

    /// <summary>
    /// The original converted for <paramref name="platform"/>, as the start of an own version: each step as it runs there,
    /// authored there; a step with no guess is kept as it is, so it still says what it needs.
    /// </summary>
    public IReadOnlyList<CommandStep> ConvertedFor(HostPlatform platform) => PlatformSides.Converted(Steps, platform);

    /// <summary>
    /// Back to running the converted original on the own version's platform: "Copy from" on the own version's side (plan 0010
    /// decision 5), which becomes inferred again and follows the original from then on.
    /// </summary>
    public Command WithoutOwnVersion() => this with { OwnVersion = null };

    /// <summary>
    /// "Copy from" on the original's side (plan 0010 decisions 5 and 8): the own version's steps become the original, each
    /// keeping the platform it was authored on, and the own version goes, so the side the original ran on becomes inferred
    /// (converted from them) and follows them from then on. The trigger the own version's platform used (its own, else the
    /// original's converted there) becomes the original's, so that platform keeps firing on what it fired on. Unchanged
    /// without an own version.
    /// </summary>
    public Command WithOwnVersionAsOriginal()
        => OwnVersion is { } own ? this with { Steps = own.Steps, Trigger = TriggerFor(own.Platform), OwnVersion = null } : this;

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
}
