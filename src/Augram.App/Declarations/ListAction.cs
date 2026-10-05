namespace Augram.App.Declarations;

/// <summary>A toolbar, context-menu or key action on a list. <see cref="Execute"/> receives the selected row (null when none).</summary>
public sealed record ListAction(string Label, Action<object?> Execute)
{
    public ListAction(string label, Action execute)
        : this(label, _ => execute())
    {
    }
}
