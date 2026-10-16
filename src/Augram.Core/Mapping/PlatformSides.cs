using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// An item of a list kept per platform the way a command's steps are (F8, plan 0010): authored on one platform, and with a best
/// guess for the other. <see cref="CommandStep"/> and <see cref="WhileHoldingInput"/> (plan 0012) are the two.
/// </summary>
public interface IPlatformItem<TSelf>
    where TSelf : class, IPlatformItem<TSelf>
{
    /// <summary>The platform the item was made on, in that platform's terms.</summary>
    HostPlatform AuthoredOn { get; }

    /// <summary>The item made for <paramref name="platform"/> from its best guess, authored there; null when it has no guess there.</summary>
    TSelf? ConvertedTo(HostPlatform platform);
}

/// <summary>
/// The rules of a list kept per platform (F8, Joel 2026-10-07; plan 0010; plan 0012 decision 5), the one home of them: a list
/// has an <b>original</b>, authored on its <see cref="Origin"/>, and at most one <b>own version</b> for the other platform. A
/// platform without an own version, other than the origin, is <b>inferred</b>: it shows the original converted and follows it.
/// An edit there makes it its own (<see cref="EditsOriginal"/> false); "Copy from" goes back. <see cref="Command"/>'s steps
/// (with <see cref="CommandVersion"/>) and an Exclusions › While holding entry's inputs (<see cref="PlatformList{T}"/>) both
/// ask these, so the two never drift. Pure; nothing allocates but <see cref="Converted"/>.
/// </summary>
public static class PlatformSides
{
    /// <summary>
    /// The platform the original was authored on: the other one than the own version's, else the first item's; null for an
    /// empty original with no own version (its first item makes that platform the origin).
    /// </summary>
    public static HostPlatform? Origin<T>(IReadOnlyList<T> original, HostPlatform? ownPlatform)
        where T : class, IPlatformItem<T>
    {
        ArgumentNullException.ThrowIfNull(original);
        return ownPlatform is { } own ? own.Other() : original.Count > 0 ? original[0].AuthoredOn : null;
    }

    /// <summary>The list <paramref name="platform"/> shows: its own version's, else the original (as stored, not converted).</summary>
    public static IReadOnlyList<T> For<T>(HostPlatform platform, IReadOnlyList<T> original, HostPlatform? ownPlatform, IReadOnlyList<T>? own)
        where T : class, IPlatformItem<T>
        => ownPlatform == platform && own is not null ? own : original;

    /// <summary>True when an edit on <paramref name="platform"/> changes the original: it is the origin, or there is none yet.</summary>
    public static bool EditsOriginal(HostPlatform? origin, HostPlatform platform) => origin is not { } from || from == platform;

    /// <summary>True when <paramref name="platform"/> follows the original converted: it is not the origin and has no own version.</summary>
    public static bool IsInferred(HostPlatform? origin, HostPlatform? ownPlatform, HostPlatform platform)
        => origin is { } from && from != platform && ownPlatform != platform;

    /// <summary>
    /// The original converted for <paramref name="platform"/>, as the start of an own version: each item as it reads there,
    /// authored there; an item with no guess is kept as it is, so it still says what it needs.
    /// </summary>
    public static IReadOnlyList<T> Converted<T>(IReadOnlyList<T> original, HostPlatform platform)
        where T : class, IPlatformItem<T>
    {
        ArgumentNullException.ThrowIfNull(original);
        return [.. original.Select(item => item.AuthoredOn != platform && item.ConvertedTo(platform) is { } converted ? converted : item)];
    }
}

/// <summary>An own version of a <see cref="PlatformList{T}"/>: the list <paramref name="Platform"/> uses instead of the original converted.</summary>
public sealed record PlatformVersion<T>(HostPlatform Platform, IReadOnlyList<T> Items)
    where T : class, IPlatformItem<T>;

/// <summary>
/// A list kept per platform with no more than the rules of <see cref="PlatformSides"/>: the <see cref="Original"/> and an
/// optional <see cref="Own"/> version (plan 0012: an Exclusions › While holding entry's inputs). A command keeps its steps the
/// same way but in its own members (<see cref="Command.Steps"/>, <see cref="Command.OwnVersion"/>, which also holds the own
/// trigger and the stale fingerprint), asking the same rules. Immutable.
/// </summary>
public sealed record PlatformList<T>(IReadOnlyList<T> Original, PlatformVersion<T>? Own)
    where T : class, IPlatformItem<T>
{
    /// <summary>No items on either platform.</summary>
    public static PlatformList<T> Empty { get; } = new([], null);

    public bool IsEmpty => Original.Count == 0 && Own is null;

    /// <summary>The platform the original was authored on (<see cref="PlatformSides.Origin"/>); null while it is empty and nothing is own.</summary>
    public HostPlatform? Origin => PlatformSides.Origin(Original, Own?.Platform);

    /// <summary>The list <paramref name="platform"/> shows as stored: its own version's, else the original.</summary>
    public IReadOnlyList<T> For(HostPlatform platform) => PlatformSides.For(platform, Original, Own?.Platform, Own?.Items);

    /// <summary>True when <paramref name="platform"/> has an own version.</summary>
    public bool HasOwnOn(HostPlatform platform) => Own?.Platform == platform;

    /// <summary>True when <paramref name="platform"/> follows the original converted (<see cref="PlatformSides.IsInferred"/>).</summary>
    public bool IsInferredOn(HostPlatform platform) => PlatformSides.IsInferred(Origin, Own?.Platform, platform);

    /// <summary>The original converted for <paramref name="platform"/> (<see cref="PlatformSides.Converted"/>).</summary>
    public IReadOnlyList<T> ConvertedFor(HostPlatform platform) => PlatformSides.Converted(Original, platform);

    /// <summary>The list an edit on <paramref name="platform"/> starts from: the original converted where it is inferred, else <see cref="For"/>.</summary>
    public IReadOnlyList<T> EditableFor(HostPlatform platform) => IsInferredOn(platform) ? ConvertedFor(platform) : For(platform);

    /// <summary>
    /// <paramref name="items"/> as <paramref name="platform"/>'s list: the original where it was authored (or while there is no
    /// original), else the own version, made by the first edit there.
    /// </summary>
    public PlatformList<T> WithFor(HostPlatform platform, IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return PlatformSides.EditsOriginal(Origin, platform) ? this with { Original = items } : this with { Own = new PlatformVersion<T>(platform, items) };
    }

    /// <summary>"Copy from" on the own version's side (plan 0010 decision 5): it becomes inferred again and follows the original.</summary>
    public PlatformList<T> WithoutOwn() => this with { Own = null };

    /// <summary>
    /// "Copy from" on the original's side: the own version's items become the original, each keeping the platform it was
    /// authored on, and the side the original ran on becomes inferred. Unchanged without an own version.
    /// </summary>
    public PlatformList<T> WithOwnAsOriginal() => Own is { } own ? new PlatformList<T>(own.Items, null) : this;

    /// <summary>
    /// "Copy from the other platform" on <paramref name="platform"/>'s side, whole list: <see cref="WithoutOwn"/> on the own
    /// version's side, <see cref="WithOwnAsOriginal"/> on the original's; unchanged without an own version (nothing differs).
    /// </summary>
    public PlatformList<T> WithOtherPlatformCopied(HostPlatform platform)
        => Own is not { } own ? this : own.Platform == platform ? WithoutOwn() : WithOwnAsOriginal();
}
