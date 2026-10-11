using System.Runtime.CompilerServices;
using Augram.Core.Config;

namespace Augram.App.Declarations;

/// <summary>
/// An RGB colour (F6: trail colour). Binds the Core value type so no conversion lives in the UI. Without
/// <see cref="Presets"/> the editor is a swatch that opens the colour picker, with the three channels beside it; with them
/// (plan 0006 decision 8) it is a row of round swatches in their order, the current colour ringed (a colour that is no
/// preset as one more swatch at the end), then Custom…, which opens the same picker.
/// </summary>
public sealed record ColorField(
    string Label,
    IValueBinding<RgbColor> Value,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Color";

    public override IValueBinding Binding => Value;

    /// <summary>The swatches to offer, in order (<see cref="ColourPresets.Rainbow"/>); null or empty (the default) for the plain editor.</summary>
    public IReadOnlyList<ColourPreset>? Presets { get; init; }
}
