namespace Augram.App.Components.HeldInputList;

/// <summary>
/// What one row of the <see cref="HeldInputList"/> shows (plan 0012): the input's position, its name ("Alt", "S", "Middle";
/// "Ctrl → Cmd" when converted from the other platform), its F8 marker ("from Windows · converted", "from Windows · needs a
/// macOS version"; null for an input authored on the side shown) and the hold-key warning (decision 10; null for none).
/// Built by the view model (<c>ViewModels/Ignored/IgnoredViewModel.WhileHolding</c>).
/// </summary>
public sealed record HeldInputItem(int Index, string Name, string? Marker, string? Warning)
{
    public bool HasMarker => !string.IsNullOrEmpty(Marker);

    public bool HasWarning => !string.IsNullOrEmpty(Warning);
}
