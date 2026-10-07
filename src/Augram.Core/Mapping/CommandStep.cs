using Augram.Core.Abstractions;
using Augram.Core.Steps;

namespace Augram.Core.Mapping;

/// <summary>
/// One step of a command as stored: the step as authored and the platform it was authored on (F8). <see cref="ForPlatform"/>
/// says what runs on a platform: the step itself where it was authored, else the type's best-guess conversion (or none,
/// with the reason). A platform that needs different steps gets a <see cref="CommandVersion"/> of the whole command, not a
/// per-step override (Joel, 2026-10-07). Inactive steps stay in the list and are skipped by the executor.
/// </summary>
public sealed record CommandStep(
    IStep Step,
    HostPlatform AuthoredOn,
    bool IsActive = true)
{
    /// <summary>What runs on <paramref name="platform"/>: the step itself where it was authored, else the step type's best guess, which may be "none" with a reason.</summary>
    public StepConversion ForPlatform(HostPlatform platform)
        => platform == AuthoredOn ? StepConversion.Same(Step) : Step.Type.Convert(Step, AuthoredOn, platform);
}
