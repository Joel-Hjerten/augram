using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// A declared list (ADR-0002 §5c), rendered by <c>ItemList</c>. Rows come from <see cref="Source"/>;
/// <see cref="Columns"/> render text cells unless <see cref="RowComponent"/> supplies a control per row.
/// <see cref="Filters"/> are applied by the renderer; <see cref="Toolbar"/>, <see cref="ContextMenu"/>
/// and <see cref="Keymap"/> are the list's actions. <see cref="Ordering"/> is declared for the Gestures
/// and step lists; drag-reordering itself lands with those screens.
/// </summary>
public sealed record ListSpec(
    string Title,
    IReadOnlyList<ListColumn> Columns,
    IListSource Source,
    IReadOnlyList<ListAction>? Toolbar = null,
    IReadOnlyList<ListFilter>? Filters = null,
    IReadOnlyList<ListAction>? ContextMenu = null,
    IReadOnlyList<ListKey>? Keymap = null,
    ListOrdering Ordering = ListOrdering.Sorted,
    bool AutoScroll = false,
    Func<object, Control>? RowComponent = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0)
{
    /// <summary>Where the spec was declared (named apart from <see cref="Source"/>, the rows).</summary>
    public SourceLocation Declared => new(File, Line);
}
