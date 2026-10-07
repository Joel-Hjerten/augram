using Augram.Core.Abstractions;
using Augram.Core.Steps;

namespace Augram.Core.Mapping;

/// <summary>
/// One step of a command as stored (the F8 envelope): the step as authored, the platform it was
/// authored on, and an optional hand-made replacement per platform. <see cref="ForPlatform"/> says what
/// runs on a platform: its override when there is one, the step itself where it was authored, else the
/// type's best-guess conversion (or none, with the reason). The original is never edited by an override,
/// so an export still works on both platforms. Inactive steps stay in the list and are skipped by the executor.
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

    /// <summary>
    /// What runs on <paramref name="platform"/> (F8): its override as is, the step itself on the platform it was authored
    /// on, else the step type's best guess, which may be "none" with a reason. The executor and the step list both ask this.
    /// </summary>
    public StepConversion ForPlatform(HostPlatform platform)
        => OverrideFor(platform) is { } own ? StepConversion.Same(own)
            : platform == AuthoredOn ? StepConversion.Same(Step)
            : Step.Type.Convert(Step, AuthoredOn, platform);

    /// <summary>A copy with the override for <paramref name="platform"/> replaced (null removes it).</summary>
    public CommandStep WithOverride(HostPlatform platform, IStep? step) => platform switch
    {
        HostPlatform.Windows => this with { WindowsOverride = step },
        HostPlatform.MacOS => this with { MacOsOverride = step },
        _ => this,
    };
}
