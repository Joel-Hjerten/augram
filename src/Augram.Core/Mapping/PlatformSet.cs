using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// The platforms an app group takes part on (F8 "Use on", Joel 2026-10-07): a group not used on a platform never matches
/// there and is hidden from that platform's list unless the user asks to see the other platforms' groups. Written to
/// the file as a list of target names (<c>["windows"]</c>) so specific machines can join the list later without a new format.
/// </summary>
[Flags]
public enum PlatformSet
{
    None = 0,
    Windows = 1,
    MacOS = 2,
    All = Windows | MacOS,
}

/// <summary>Reading a <see cref="PlatformSet"/> against one <see cref="HostPlatform"/>.</summary>
public static class PlatformSetExtensions
{
    public static PlatformSet ToSet(this HostPlatform platform) => platform == HostPlatform.MacOS ? PlatformSet.MacOS : PlatformSet.Windows;

    public static bool Includes(this PlatformSet set, HostPlatform platform) => (set & platform.ToSet()) != 0;

    public static PlatformSet With(this PlatformSet set, HostPlatform platform, bool included)
        => included ? set | platform.ToSet() : set & ~platform.ToSet();
}
