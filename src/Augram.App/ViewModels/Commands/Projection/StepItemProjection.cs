using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands.Projection;

/// <summary>A <see cref="CommandStep"/> as one row of the step list shows it (<see cref="StepItem"/>), in <see cref="StepPlatformMarker"/>'s F8 words.</summary>
public static class StepItemProjection
{
    /// <summary>The step at <paramref name="index"/> as it reads on <paramref name="here"/> (F8: converted or from the other platform).</summary>
    public static StepItem From(CommandStep step, int index, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(step);
        var text = StepPlatformMarker.For(step, here);
        return new StepItem(index, step, text.Summary, step.Step.Type.DisplayName, step.IsActive, text.Marker);
    }
}
