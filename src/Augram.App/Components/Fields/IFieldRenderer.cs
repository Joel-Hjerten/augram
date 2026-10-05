using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields;

/// <summary>
/// Builds the editor control for one field kind. One implementation per folder under
/// <c>Components/Fields/</c>; <see cref="FieldRendererRegistry"/> finds them by scanning the assembly,
/// so adding a kind is adding a folder (ADR-0002 §5c). Implementations need a parameterless constructor.
/// </summary>
public interface IFieldRenderer
{
    /// <summary>Matches <see cref="Field.Kind"/>.</summary>
    string Kind { get; }

    Control Build(Field field);
}
