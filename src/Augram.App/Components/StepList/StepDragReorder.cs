using Avalonia;
using Avalonia.Input;

namespace Augram.App.Components.StepList;

/// <summary>
/// Left-button press tracking for the step list (F5a): the list hands it a press on a row's header or grip
/// (<see cref="Press"/>), and the press ends as a click or a drag. A release without moving past
/// <see cref="ThresholdPx"/> is a click, reported for a header press only: the grip only drags (Joel, 0.11.4). A move past
/// it starts the drag (the row captures the pointer, is marked <c>:dragging</c> and reported as started), the row under the
/// pointer is marked <c>:drop-target</c>, and a release over another row calls back with (from, to) indexes. The host moves
/// the step; this class never does. Positions are measured in the surface's coordinates (the list), so rows may be any height.
/// </summary>
internal sealed class StepDragReorder
{
    public const double ThresholdPx = 6;

    private readonly Func<IReadOnlyList<StepRow>> _rows;
    private readonly Func<Visual?> _surface;
    private readonly Action<int, int> _reorder;
    private readonly Action<StepRow> _started;
    private readonly Action<StepRow> _clicked;
    private StepRow? _pressed;
    private StepRow? _target;
    private Point _start;
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
            pressed.SetDragging(true);
            e.Pointer.Capture(pressed);
            _started(pressed);
            if (_pressed is null)
            {
                return;
            }
        }

        SetTarget(RowAt(position, surface));
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressed;
        var target = _target;
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
        if (dragging && target is not null && pressed.Item is { } from && target.Item is { } to && from.Index != to.Index)
        {
            _reorder(from.Index, to.Index);
        }
        else if (!dragging && clickable)
        {
            _clicked(pressed);
        }
    }

    private StepRow? RowAt(Point position, Visual surface)
    {
        foreach (var row in _rows())
        {
            if (row.TranslatePoint(new Point(0, 0), surface) is { } top && position.Y >= top.Y && position.Y < top.Y + row.Bounds.Height)
            {
                return row;
            }
        }

        return null;
    }

    private void SetTarget(StepRow? target)
    {
        if (ReferenceEquals(_target, target))
        {
            return;
        }

        _target?.SetDropTarget(false);
        _target = target;
        _target?.SetDropTarget(true);
    }

    private void Reset()
    {
        if (_pressed is null)
        {
            return;
        }

        _pressed.SetDragging(false);
        SetTarget(null);
        _pressed = null;
        _dragging = false;
        _clickable = false;
    }
}
