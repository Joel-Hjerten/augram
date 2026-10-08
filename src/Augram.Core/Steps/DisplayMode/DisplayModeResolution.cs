using Augram.Core.Abstractions;

namespace Augram.Core.Steps.DisplayMode;

/// <summary>
/// What <see cref="DisplayModeResolver"/> made of a step's targets on one display: a <see cref="Mode"/> to apply, the
/// same mode with <see cref="IsCurrent"/> when the display already runs it, or no mode and the <see cref="Reason"/>
/// the step is skipped with (it names what the display does offer).
/// </summary>
public sealed record DisplayModeResolution(VideoMode? Mode, bool IsCurrent, string? Reason)
{
    public static DisplayModeResolution Change(VideoMode mode) => new(mode, false, null);

    public static DisplayModeResolution Current(VideoMode mode) => new(mode, true, null);

    public static DisplayModeResolution Unsupported(string reason) => new(null, false, reason);
}
