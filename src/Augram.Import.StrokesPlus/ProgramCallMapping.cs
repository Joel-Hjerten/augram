using Augram.Core.Steps;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Where a recognised <c>sp.RunProgram</c> call goes (plan 0001 §C1): a Run step through <see cref="RunMapping"/>. Shared by
/// a fresh import (<see cref="ActionReader"/>) and the upgrade of saved placeholders (<see cref="PlaceholderUpgrade"/>), so
/// both route the same call to the same step.
/// </summary>
public static class ProgramCallMapping
{
    /// <summary>The step that does what <paramref name="call"/> did.</summary>
    public static IStep ToStep(RunProgramCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return RunMapping.FromRunProgram(call);
    }
}
