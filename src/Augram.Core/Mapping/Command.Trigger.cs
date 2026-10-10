using Augram.Core.Abstractions;
using Augram.Core.Gestures;

namespace Augram.Core.Mapping;

/// <summary>
/// The trigger half of <see cref="Command"/> across platforms (F8 with Joel's 2026-10-09 decisions): <see cref="Trigger"/> is
/// the original, authored where the steps were (<see cref="Origin"/>); another platform uses its own version's trigger when it
/// has one, else the original with its keys converted like a hotkey's (<see cref="TriggerConversion.Convert"/>). Editing the
/// trigger on the other platform makes it that platform's own (<see cref="WithTriggerFor"/>).
/// </summary>
public sealed partial record Command
{
    /// <summary>The trigger as it reads on <paramref name="platform"/>: its own, the original, or the original converted (or none, with the reason).</summary>
    public TriggerConversion TriggerPlanFor(HostPlatform platform)
    {
        if (OwnVersion is { Trigger: { } own } version && version.Platform == platform)
        {
            return TriggerConversion.Same(own);
        }

        return Origin is { } origin && origin != platform
            ? TriggerConversion.Convert(Trigger, origin, platform)
            : TriggerConversion.Same(Trigger);
    }

    /// <summary>What fires the command on <paramref name="platform"/>; <see cref="Trigger.None"/> when the original has no counterpart there.</summary>
    public Trigger TriggerFor(HostPlatform platform) => TriggerPlanFor(platform).Trigger ?? Trigger.None;

    /// <summary>True when <paramref name="platform"/> has a trigger of its own (its own version made one).</summary>
    public bool HasOwnTriggerOn(HostPlatform platform) => OwnVersion is { Trigger: not null } own && own.Platform == platform;

    /// <summary>
    /// The command with <paramref name="trigger"/> as <paramref name="platform"/>'s trigger: the original where it was authored
    /// (or when there is no original yet), else the own version's, which is made on the first such edit from the converted
    /// original steps (F8: the whole command forks), so the edit counts as having checked them.
    /// </summary>
    public Command WithTriggerFor(HostPlatform platform, Trigger trigger, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (Origin is not { } origin || origin == platform)
        {
            return this with { Trigger = trigger };
        }

        var own = OwnVersion is { } existing && existing.Platform == platform
            ? existing with { ChangedAt = now }
            : new CommandVersion(platform, ConvertedFor(platform), Fingerprint(Steps), now);
        return this with { OwnVersion = own with { Trigger = trigger } };
    }

    /// <summary>True when the original trigger or an own version's names the gesture (the "Used by…" list, the delete warning).</summary>
    public bool UsesGesture(GestureId gestureId)
        => Names(Trigger, gestureId) || (OwnVersion?.Trigger is { } own && Names(own, gestureId));

    /// <summary>
    /// The command with every trigger naming <paramref name="from"/> (the original's and an own version's) moved to
    /// <paramref name="to"/>, keeping what it holds, or unbound when <paramref name="to"/> is null (the gesture is deleted).
    /// </summary>
    public Command WithGestureReplaced(GestureId from, GestureId? to)
    {
        var command = this;
        if (Names(Trigger, from))
        {
            command = command with { Trigger = Replaced(Trigger, to) };
        }

        if (OwnVersion is { Trigger: { } own } version && Names(own, from))
        {
            command = command with { OwnVersion = version with { Trigger = Replaced(own, to) } };
        }

        return command;
    }

    /// <summary>The gestures the original trigger and an own version's name, each once: what an export of the command carries along (plan 0003).</summary>
    public IReadOnlyList<GestureId> GestureIds()
    {
        var ids = new List<GestureId>(2);
        if (Trigger is Trigger.GestureTrigger gesture)
        {
            ids.Add(gesture.GestureId);
        }

        if (OwnVersion?.Trigger is Trigger.GestureTrigger own && !ids.Contains(own.GestureId))
        {
            ids.Add(own.GestureId);
        }

        return ids;
    }

    /// <summary>
    /// The command with each trigger (the original's and an own version's, each on its own, so one replacement never feeds
    /// another) naming a gesture <paramref name="map"/> has moved to the mapped id, keeping what it holds: an import's id
    /// map (StrokesPlus.net, Augram JSON, plan 0003).
    /// </summary>
    public Command WithGesturesReplaced(IReadOnlyDictionary<GestureId, GestureId> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var command = Trigger is Trigger.GestureTrigger gesture && map.TryGetValue(gesture.GestureId, out var to)
            ? this with { Trigger = gesture with { GestureId = to } }
            : this;
        return OwnVersion is { Trigger: Trigger.GestureTrigger own } version && map.TryGetValue(own.GestureId, out var ownTo)
            ? command with { OwnVersion = version with { Trigger = own with { GestureId = ownTo } } }
            : command;
    }

    private static Trigger Replaced(Trigger trigger, GestureId? to)
        => to is { } id && trigger is Trigger.GestureTrigger gesture ? gesture with { GestureId = id } : Trigger.None;

    private static bool Names(Trigger trigger, GestureId gestureId)
        => trigger is Trigger.GestureTrigger gesture && gesture.GestureId == gestureId;
}
