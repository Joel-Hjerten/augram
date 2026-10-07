using Avalonia;
using Avalonia.Input;

namespace Augram.App.Components.StepList;

/// <summary>
/// Left-button drag reorder for the step list (F5a): a press on a row arms it, a move past
/// <see cref="ThresholdPx"/> starts the drag (the row captures the pointer and is marked
/// <c>:dragging</c>), the row under the pointer is marked <c>:drop-target</c>, and a release over
/// another row calls back with (from, to) indexes. The host moves the step; this class never does.
/// Positions are measured in the surface's coordinates (the list), so rows may be any height.
/// </summary>
internal sealed class StepDragReorder
{
    public const double ThresholdPx = 6;

    private readonly Func<IReadOnlyList<StepRow>> _rows;
    private readonly Func<Visual?> _surface;
    private readonly Action<int, int> _reorder;
    private StepRow? _pressed;
    private StepRow? _target;
    private Point _start;
    private bool _dragging;

    public StepDragReorder(Func<IReadOnlyList<StepRow>> rows, Func<Visual?> surface, Action<int, int> reorder)
    {
        _rows = rows;
        _surface = surface;
        _reorder = reorder;
    }

    public bool IsDragging => _dragging;

    public void Attach(StepRow row)
    {
        row.PointerPressed += OnPressed;
        row.PointerMoved += OnMoved;
        row.PointerReleased += OnReleased;
        row.PointerCaptureLost += (_, _) => Reset();
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not StepRow row || _surface() is not { } surface || !e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = row;
        _start = e.GetPosition(surface);
        _dragging = false;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null || _surface() is not { } surface)
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
            _pressed.SetDragging(true);
            e.Pointer.Capture(_pressed);
        }

        SetTarget(RowAt(position, surface));
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressed;
        var target = _target;
        var dragging = _dragging;
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
    }
}
