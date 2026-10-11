using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Augram.App.Components.StepList;

/// <summary>
/// Left-button press tracking for the step list (F5a): the list hands it a press on a row's header or grip
/// (<see cref="Press"/>), and the press ends as a click or a drag. A release without moving past
/// <see cref="ThresholdPx"/> is a click, reported for a header press only: the grip only drags (Joel, 0.11.4). A move past
/// it starts the drag (the row captures the pointer, is marked <c>:dragging</c> and reported as started). While it drags the
/// list shows the order a release would give (Joel, 0.11.11: "they should reorganize immediately and not after release"): the
/// dragged row follows the pointer above the others, and the rows between its place and the target slide aside to make room
/// (render offsets only, <see cref="StepRow.DragOffset"/>; nothing is stored). A release calls back once with (from, to)
/// indexes, so a drag is one undo step; a lost capture (Escape, another window) puts every row back. The host moves the step;
/// this class never does. Positions are measured in the surface's coordinates (the list), so rows may be any height.
/// </summary>
internal sealed class StepDragReorder
{
    public const double ThresholdPx = 6;

    private static readonly TimeSpan Slide = TimeSpan.FromMilliseconds(120);

    private readonly Func<IReadOnlyList<StepRow>> _rows;
    private readonly Func<Visual?> _surface;
    private readonly Action<int, int> _reorder;
    private readonly Action<StepRow> _started;
    private readonly Action<StepRow> _clicked;
    private readonly List<StepRow> _shifted = [];
    private StepRow? _pressed;
    private Point _start;
    private double _grab;
    private int _to = -1;
    private bool _clickable;
    private bool _dragging;

    public StepDragReorder(Func<IReadOnlyList<StepRow>> rows, Func<Visual?> surface, Action<int, int> reorder, Action<StepRow> started, Action<StepRow> clicked)
    {
        _rows = rows;
        _surface = surface;
        _reorder = reorder;
        _started = started;
        _clicked = clicked;
    }

    public bool IsDragging => _dragging;

    public void Attach(StepRow row)
    {
        row.PointerMoved += OnMoved;
        row.PointerReleased += OnReleased;
        row.PointerCaptureLost += (_, _) => Reset();
    }

    /// <summary>Arms the tracker for a left press on <paramref name="row"/>; <paramref name="clickable"/> is false for the grip, whose click is nothing.</summary>
    public void Press(StepRow row, PointerEventArgs e, bool clickable)
    {
        if (_surface() is not { } surface)
        {
            return;
        }

        _pressed = row;
        _start = e.GetPosition(surface);
        _clickable = clickable;
        _dragging = false;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is not { } pressed || _surface() is not { } surface)
        {
            return;
        }

        var position = e.GetPosition(surface);
        if (!_dragging)
        {
            if (Math.Abs(position.X - _start.X) < ThresholdPx && Math.Abs(position.Y - _start.Y) < ThresholdPx)
            {
                return;
            }

            _dragging = true;
            _grab = _start.Y - RestTop(pressed, surface);
            pressed.SetDragging(true);
            e.Pointer.Capture(pressed);
            _started(pressed);
            if (_pressed is null)
            {
                return;
            }
        }

        Arrange(pressed, position.Y, surface);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressed;
        var to = _to;
        var dragging = _dragging;
        var clickable = _clickable;
        if (pressed is null)
        {
            return;
        }

        if (ReferenceEquals(e.Pointer.Captured, pressed))
        {
            e.Pointer.Capture(null);
        }

        Reset();
        if (dragging && pressed.Item is { } from && to >= 0 && from.Index != to)
        {
            _reorder(from.Index, to);
        }
        else if (!dragging && clickable)
        {
            _clicked(pressed);
        }
    }

    /// <summary>
    /// Shows the order a release at <paramref name="pointerY"/> gives: the dragged row's top follows the pointer (kept within the
    /// list), its target is the number of other rows whose middle is above its own, and the rows it passed slide one slot (its
    /// height plus the gap between rows) the other way. A tie with a row's middle counts as passing it, toward either end.
    /// </summary>
    private void Arrange(StepRow pressed, double pointerY, Visual surface)
    {
        var rows = _rows();
        var from = IndexOf(rows, pressed);
        if (from < 0)
        {
            return;
        }

        var tops = rows.Select(row => RestTop(row, surface)).ToArray();
        var heights = rows.Select(row => Host(row).Bounds.Height).ToArray();
        var height = heights[from];
        var lowest = tops[^1] + heights[^1] - height;
        var top = Math.Clamp(pointerY - Math.Clamp(_grab, 0, height), tops[0], Math.Max(tops[0], lowest));
        var middle = top + (height / 2);
        var to = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            // A row stays above the dragged one while its middle is above the dragged middle; a tie gives way in the direction of the
            // drag, so a step pushed against either end of the list (where its top is held) passes the end step (Joel, 0.11.13: the
            // top step never made way).
            var rowMiddle = tops[i] + (heights[i] / 2);
            if (i < from ? rowMiddle < middle : i > from && rowMiddle <= middle)
            {
                to++;
            }
        }

        var gap = rows.Count > 1 ? Math.Max(0, tops[1] - tops[0] - heights[0]) : 0;
        var slot = height + gap;
        for (var i = 0; i < rows.Count; i++)
        {
            var offset = i == from ? top - tops[from]
                : from < to && i > from && i <= to ? -slot
                : to < from && i >= to && i < from ? slot
                : 0;
            Shift(rows[i], offset, lifted: i == from);
        }

        _to = to;
    }

    /// <summary>
    /// What a drag moves for <paramref name="row"/>: its list item, not the row itself. The item clips what it holds, so a row
    /// offset inside it vanished as soon as it left the item's bounds (Joel, 0.11.12); the items panel clips nothing.
    /// </summary>
    private static Control Host(StepRow row) => row.FindAncestorOfType<ListBoxItem>() ?? (Control)row;

    /// <summary>Where the row's item sits without the offset this drag gave it.</summary>
    private static double RestTop(StepRow row, Visual surface)
    {
        var host = Host(row);
        return (host.TranslatePoint(new Point(0, 0), surface)?.Y ?? 0) - ((host.RenderTransform as TranslateTransform)?.Y ?? 0);
    }

    private void Shift(StepRow row, double offset, bool lifted)
    {
        var host = Host(row);
        if (host.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            host.RenderTransform = transform;
        }

        // The dragged row is under the pointer, so it moves at once; the others slide.
        transform.Transitions = lifted ? null : [new DoubleTransition { Property = TranslateTransform.YProperty, Duration = Slide, Easing = new CubicEaseOut() }];
        transform.Y = offset;
        row.DragOffset = offset;
        host.ZIndex = lifted ? 1 : 0;

        if (!_shifted.Contains(row))
        {
            _shifted.Add(row);
        }
    }

    private static int IndexOf(IReadOnlyList<StepRow> rows, StepRow row)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row))
            {
                return i;
            }
        }

        return -1;
    }

    private void Reset()
    {
        foreach (var row in _shifted)
        {
            var host = Host(row);
            host.RenderTransform = null;
            host.ZIndex = 0;
            row.DragOffset = 0;
        }

        _shifted.Clear();
        _to = -1;
        if (_pressed is null)
        {
            return;
        }

        _pressed.SetDragging(false);
        _pressed = null;
        _dragging = false;
        _clickable = false;
    }
}
