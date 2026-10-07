using Augram.Core.Abstractions;
using Augram.Core.Steps;

namespace Augram.Core.Mapping;

/// <summary>
/// One step of a command as stored (the F8 envelope): the step as authored, the platform it was
/// authored on, and an optional hand-made replacement per platform. <see cref="ResolveFor"/> gives the
/// step to run on a platform: its override when there is one, else the authored step (the executor
/// then applies the type's auto-conversion when the platforms differ). The original is never edited
/// by an override, so an export still works on both platforms. Inactive steps stay in the list and
/// are skipped by the executor.
/// </summary>
public sealed record CommandStep(
    IStep Step,
    HostPlatform AuthoredOn,
    IStep? WindowsOverride = null,
    IStep? MacOsOverride = null,
    bool IsActive = true)
{
    public bool HasOverrides => WindowsOverride is not null || MacOsOverride is not null;

    public IStep? OverrideFor(HostPlatform platform) => platform switch
    {
        HostPlatform.Windows => WindowsOverride,
        HostPlatform.MacOS => MacOsOverride,
        _ => null,
    };

    /// <summary>The override for <paramref name="platform"/> when there is one, else <see cref="Step"/>.</summary>
    public IStep ResolveFor(HostPlatform platform) => OverrideFor(platform) ?? Step;

    /// <summary>A copy with the override for <paramref name="platform"/> replaced (null removes it).</summary>
    public CommandStep WithOverride(HostPlatform platform, IStep? step) => platform switch
    {
        HostPlatform.Windows => this with { WindowsOverride = step },
        HostPlatform.MacOS => this with { MacOsOverride = step },
        _ => this,
    };
}
