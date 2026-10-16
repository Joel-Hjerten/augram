namespace Augram.App.Components.ListEditing;

/// <summary>
/// A row <see cref="RowDragReorder{TRow}"/> can drag: where its item sits in the list, the drag's render offset and the
/// <c>:dragging</c> mark. The step list's <c>StepRow</c> and the While holding list's <c>HeldInputRow</c> (plan 0012).
/// </summary>
internal interface IReorderRow
{
    /// <summary>How far a press must move, either way, before it is a drag and not a click.</summary>
    const double DragThresholdPx = 6;

    /// <summary>The row's item's index in the list; -1 while it shows none.</summary>
    int ReorderIndex { get; }

    /// <summary>How far a drag in progress shows the row from its place, in pixels; 0 at rest.</summary>
    double DragOffset { get; set; }

    void SetDragging(bool dragging);
}
