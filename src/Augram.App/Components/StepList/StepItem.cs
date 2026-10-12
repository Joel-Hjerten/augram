using Augram.Core.Mapping;

namespace Augram.App.Components.StepList;

/// <summary>
/// What one row of the <see cref="StepList"/> shows: the step's position, its one-line summary, its type's name, the active flag
/// and the F8 marker. A projection of a <see cref="CommandStep"/>, built by the view model (<c>ViewModels/Commands/Projection/StepItemProjection</c>).
/// </summary>
public sealed record StepItem(int Index, CommandStep Step, string Summary, string TypeName, bool IsActive, string? PlatformMarker)
{
    public bool HasMarker => !string.IsNullOrEmpty(PlatformMarker);
}
