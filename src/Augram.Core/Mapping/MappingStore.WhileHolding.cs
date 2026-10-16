using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// The edits of an Exclusions › While holding entry's "Ignore gestures while holding" list (plan 0012 decisions 2, 5 and 6),
/// one store call and one undo step each, validated like every other change. Each edits the given platform's side as a
/// command's step edits do (<see cref="PlatformList{T}.EditableFor"/>, then <see cref="PlatformList{T}.WithFor"/>): the original
/// where it was authored, the own version where there is one, and on an inferred side the first edit makes the own version from
/// the original converted. An input added here is authored here. An input already on that side is refused.
/// </summary>
public sealed partial class MappingStore
{
    /// <summary>Adds <paramref name="input"/> to <paramref name="platform"/>'s side, at <paramref name="at"/> (the end when null).</summary>
    /// <exception cref="MappingValidationException">The entry is not on While holding, or the input is already on that side.</exception>
    public IgnoredApp AddHeldInput(GroupId entryId, HostPlatform platform, KeyOrButton input, int? at = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (entry, rows) = HoldingSide(entryId, platform);
        EnsureNotListed(entry, rows, input, platform, except: -1);
        rows.Insert(at is { } index ? Math.Clamp(index, 0, rows.Count) : rows.Count, new WhileHoldingInput(input, platform));
        return CommitHolding(entry, platform, rows);
    }

    /// <summary>Removes the input at <paramref name="index"/> of <paramref name="platform"/>'s side.</summary>
    public IgnoredApp RemoveHeldInput(GroupId entryId, HostPlatform platform, int index)
    {
        var (entry, rows) = HoldingSide(entryId, platform);
        rows.RemoveAt(RequireRow(rows, index));
        return CommitHolding(entry, platform, rows);
    }

    /// <summary>Moves the input at <paramref name="from"/> to <paramref name="to"/> on <paramref name="platform"/>'s side.</summary>
    public IgnoredApp MoveHeldInput(GroupId entryId, HostPlatform platform, int from, int to)
    {
        var (entry, rows) = HoldingSide(entryId, platform);
        var moved = rows[RequireRow(rows, from)];
        rows.RemoveAt(from);
        rows.Insert(RequireRow(rows, to, allowEnd: true), moved);
        return CommitHolding(entry, platform, rows);
    }

    /// <summary>Replaces the input at <paramref name="index"/> of <paramref name="platform"/>'s side with <paramref name="input"/>, authored here.</summary>
    /// <exception cref="MappingValidationException">The input is already elsewhere on that side.</exception>
    public IgnoredApp ReplaceHeldInput(GroupId entryId, HostPlatform platform, int index, KeyOrButton input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (entry, rows) = HoldingSide(entryId, platform);
        EnsureNotListed(entry, rows, input, platform, except: RequireRow(rows, index));
        rows[index] = new WhileHoldingInput(input, platform);
        return CommitHolding(entry, platform, rows);
    }

    /// <summary>
    /// "Copy from Windows / macOS" on <paramref name="platform"/>'s side, the whole list (plan 0012 decision 6, as plan 0010
    /// decision 5 for steps): on the own version's side it becomes inferred again; on the original's side the own version
    /// becomes the original. Nothing is committed without an own version (both sides already agree).
    /// </summary>
    public IgnoredApp CopyHeldInputsFromOtherPlatform(GroupId entryId, HostPlatform platform)
    {
        var entry = RequireWhileHolding(entryId);
        if (entry.Holding.Own is null)
        {
            return entry;
        }

        return UpdateIgnored(entry with { Holding = entry.Holding.WithOtherPlatformCopied(platform) });
    }

    /// <summary>
    /// "Copy to Windows / macOS" on one row of the other platform's side (plan 0012 decision 6): the input at
    /// <paramref name="otherIndex"/> of the other side as it shows there (<see cref="PlatformList{T}.EditableFor"/>), converted
    /// for <paramref name="platform"/>, added at the end of this platform's side, which becomes its own if it was inferred.
    /// </summary>
    /// <exception cref="MappingValidationException">The input has no counterpart here ("Win needs a macOS version: …"), or it is already on this side.</exception>
    public IgnoredApp CopyHeldInputFromOtherPlatform(GroupId entryId, HostPlatform platform, int otherIndex)
    {
        var entry = RequireWhileHolding(entryId);
        var other = entry.Holding.EditableFor(platform.Other());
        var row = other[RequireRow(other, otherIndex)];
        if (row.ForPlatform(platform) is not { } input)
        {
            throw new MappingValidationException($"{row.NeedsOn(platform)}, so it cannot be copied to {platform.DisplayName()}.");
        }

        return AddHeldInput(entryId, platform, input);
    }

    private (IgnoredApp Entry, List<WhileHoldingInput> Rows) HoldingSide(GroupId entryId, HostPlatform platform)
    {
        var entry = RequireWhileHolding(entryId);
        return (entry, [.. entry.Holding.EditableFor(platform)]);
    }

    private IgnoredApp CommitHolding(IgnoredApp entry, HostPlatform platform, List<WhileHoldingInput> rows)
        => UpdateIgnored(entry with { Holding = entry.Holding.WithFor(platform, rows) });

    private IgnoredApp RequireWhileHolding(GroupId entryId)
    {
        var entry = Current.Ignored[IndexOfIgnored(entryId)];
        return entry.IsWhileHolding
            ? entry
            : throw new MappingValidationException($"'{entry.Name}' is on Exclusions › {MappingRules.ListName(entry)}: only a While holding entry has inputs to hold.");
    }

    private static void EnsureNotListed(IgnoredApp entry, List<WhileHoldingInput> rows, KeyOrButton input, HostPlatform platform, int except)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (i != except && rows[i].ForPlatform(platform) == input)
            {
                throw new MappingValidationException($"{input.Describe(platform)} is already on the {platform.DisplayName()} list of '{entry.Name}'.");
            }
        }
    }

    private static int RequireRow<T>(IReadOnlyList<T> rows, int index, bool allowEnd = false)
        => index >= 0 && (index < rows.Count || (allowEnd && index == rows.Count))
            ? index
            : throw new ArgumentOutOfRangeException(nameof(index), index, $"The list has {rows.Count} inputs.");
}
