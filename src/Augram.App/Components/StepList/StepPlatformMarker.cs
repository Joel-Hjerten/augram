using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.App.Components.StepList;

/// <summary>
/// The F8 marker text of a step row: null when the step's type is platform-neutral and it has no
/// overrides; otherwise the platform it was authored on ("Windows") and which overrides it carries
/// ("has macOS override"). A command row shows the distinct markers of its steps.
/// </summary>
public static class StepPlatformMarker
{
    public static string? For(CommandStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var parts = new List<string>(3);
        if (!step.Step.Type.IsPlatformNeutral)
        {
            parts.Add(Name(step.AuthoredOn));
        }

        if (step.WindowsOverride is not null)
        {
            parts.Add("has Windows override");
        }

        if (step.MacOsOverride is not null)
        {
            parts.Add("has macOS override");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    public static string? ForCommand(IEnumerable<CommandStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var markers = steps.Select(For).Where(marker => marker is not null).Distinct(StringComparer.Ordinal).ToList();
        return markers.Count == 0 ? null : string.Join(", ", markers);
    }

    public static string Name(HostPlatform platform) => platform == HostPlatform.MacOS ? "macOS" : "Windows";
}
