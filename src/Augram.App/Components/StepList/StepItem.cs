using Augram.Core.Mapping;

namespace Augram.App.Components.StepList;

/// <summary>What one row of the <see cref="StepList"/> shows: the step's position, its one-line summary, its type's name, the active flag and the F8 marker. A projection of a <see cref="CommandStep"/>.</summary>
public sealed record StepItem(int Index, CommandStep Step, string Summary, string TypeName, bool IsActive, string? PlatformMarker)
{
    public bool HasMarker => !string.IsNullOrEmpty(PlatformMarker);

    /// <summary>Shaped for <c>Select(StepItem.From)</c>: the step, then its position in the list.</summary>
    public static StepItem From(CommandStep step, int index)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new StepItem(index, step, step.Step.Summary, step.Step.Type.DisplayName, step.IsActive, StepPlatformMarker.For(step));
    }
}
