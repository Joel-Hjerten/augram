using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.App.Components.StepList;

/// <summary>What one row of the <see cref="StepList"/> shows: the step's position, its one-line summary, its type's name, the active flag and the F8 marker. A projection of a <see cref="CommandStep"/>.</summary>
public sealed record StepItem(int Index, CommandStep Step, string Summary, string TypeName, bool IsActive, string? PlatformMarker)
{
    public bool HasMarker => !string.IsNullOrEmpty(PlatformMarker);

    /// <summary>The step at <paramref name="index"/> as it reads on <paramref name="here"/> (F8: converted or from the other platform).</summary>
    public static StepItem From(CommandStep step, int index, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(step);
        var text = StepPlatformMarker.For(step, here);
        return new StepItem(index, step, text.Summary, step.Step.Type.DisplayName, step.IsActive, text.Marker);
    }
}
