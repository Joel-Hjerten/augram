using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// A declared list (ADR-0002 §5c), rendered by <c>ItemList</c>. Rows come from <see cref="Source"/>;
/// <see cref="Columns"/> render text cells unless <see cref="RowComponent"/> supplies a control per row.
/// <see cref="Filters"/> are applied by the renderer; <see cref="Toolbar"/> holds the list's actions. A list
/// with a menu, keys or drag-reordering is a component (the step list, the gesture grid), not a declared list.
/// </summary>
public sealed record ListSpec(
    string Title,
    IReadOnlyList<ListColumn> Columns,
    IListSource Source,
    IReadOnlyList<ListAction>? Toolbar = null,
    IReadOnlyList<ListFilter>? Filters = null,
    bool AutoScroll = false,
    Func<object, Control>? RowComponent = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0)
{
    /// <summary>Where the spec was declared (named apart from <see cref="Source"/>, the rows).</summary>
    public SourceLocation Declared => new(File, Line);
}
