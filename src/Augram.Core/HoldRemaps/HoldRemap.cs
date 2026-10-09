using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// A key that, while held, changes what other inputs do (F9, Joel 2026-10-10: "remap inputs or run commands while a key is
/// held"; plan 0002). It belongs to one app group (<see cref="AppGroup.HoldRemaps"/>) and is a parent in that group's command
/// tree: the commands under it name it by <see cref="Command.HoldRemapId"/> and have an input
/// (<see cref="Trigger.InputTrigger"/>) where other commands have a gesture. The hold key's own press is swallowed and sent
/// at its release only when it was a tap: released within <see cref="TapTimeMs"/> with nothing used meanwhile
/// (<see cref="HoldRemapMachine"/>). Immutable; changed through <see cref="MappingStore"/>.
/// </summary>
/// <param name="Id">Stable identity within the group.</param>
/// <param name="Name">What the tree shows; the hold key's name by default (<see cref="DefaultName"/>), renameable.</param>
/// <param name="HoldKey">The key held; <see cref="KeyCode.None"/> while not chosen yet (the hold remap then does nothing). Never a modifier (plan 0002 decision 2).</param>
public sealed record HoldRemap(HoldRemapId Id, string Name, KeyCode HoldKey)
{
    /// <summary>The tap time of a new hold remap: 180 ms, as in Joel's AutoHotkey script.</summary>
    public const int DefaultTapTimeMs = 180;

    /// <summary>Released within this many milliseconds of its press, with nothing used, the hold key is sent (a tap).</summary>
    public int TapTimeMs { get; init; } = DefaultTapTimeMs;

    /// <summary>An inactive hold remap is invisible to the engine: its hold key types as usual.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Where it takes part (F8 "Use on"), both by default. Like a category's, it applies to every command under it on top of
    /// the command's own (<see cref="AppGroup.IsCommandUsedOn"/>); refused when used nowhere.
    /// </summary>
    public PlatformSet UseOn { get; init; } = PlatformSet.All;

    public bool IsUsedOn(HostPlatform platform) => UseOn.Includes(platform);

    /// <summary>A new, active hold remap on <paramref name="holdKey"/>, named after it, with the default tap time.</summary>
    public static HoldRemap For(KeyCode holdKey) => new(HoldRemapId.New(), DefaultName(holdKey), holdKey);

    /// <summary>"Space", "S": the hold key's display name; "Hold remap" while no key is chosen.</summary>
    public static string DefaultName(KeyCode holdKey) => holdKey == KeyCode.None ? "Hold remap" : HotkeyText.KeyName(holdKey);
}
