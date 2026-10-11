using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>
/// A number picked on a slider, its value shown beside it with <see cref="Unit"/> after it ("62%", "12 px"): plan 0006's
/// Tint and Corner rounding. The unit is written straight after the number, so it carries its own space (<c>"%"</c>,
/// <c>" px"</c>). The slider snaps to <see cref="Step"/>. Integers bind as doubles; the view model rounds.
/// </summary>
public sealed record SliderField(
    string Label,
    IValueBinding<double> Value,
    double Min,
    double Max,
    double Step = 1,
    string Unit = "",
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Slider";

    public override IValueBinding Binding => Value;
}
