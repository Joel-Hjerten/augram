using System.Runtime.CompilerServices;
using Augram.Core.Config;

namespace Augram.App.Declarations;

/// <summary>An RGB colour (F6: trail colour). Binds the Core value type so no conversion lives in the UI.</summary>
public sealed record ColorField(
    string Label,
    IValueBinding<RgbColor> Value,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Color";

    public override IValueBinding Binding => Value;
}
