using Augram.App.Components.GestureGlyph;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Augram.App.Components.GestureDrawArea;

/// <summary>
/// Lookless training canvas (F3). Shows <see cref="Points"/> where they were drawn (no normalisation)
/// and lets the user draw a stroke with the left button: press starts, moves extend, release raises
/// <see cref="StrokeCompleted"/> with the points in this control's coordinates; the host decides what
/// to keep (training replaces the previous stroke). Strokes made with the stroke button never reach
/// this control: the hook suppresses them and the engine routes them by screen position, which is why
/// <see cref="ScreenAreaChanged"/> publishes where the canvas is in physical screen pixels.
/// </summary>
[PseudoClasses(":empty")]
public sealed class GestureDrawArea : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> PointsProperty =
        AvaloniaProperty.Register<GestureDrawArea, IReadOnlyList<GesturePoint>?>(nameof(Points));

    public static readonly StyledProperty<double> ArrowLengthProperty =
        AvaloniaProperty.Register<GestureDrawArea, double>(nameof(ArrowLength), 14);

    public static readonly StyledProperty<Geometry?> GeometryProperty =
        AvaloniaProperty.Register<GestureDrawArea, Geometry?>(nameof(Geometry));

    private readonly List<GesturePoint> _drawing = [];
    private Window? _window;
    private bool _isDrawing;

    public GestureDrawArea()
    {
        PseudoClasses.Set(":empty", true);
    }

    /// <summary>Raised on the UI thread when a left-button stroke ends. The list is a fresh copy.</summary>
    public event EventHandler<IReadOnlyList<GesturePoint>>? StrokeCompleted;

    /// <summary>Raised whenever the canvas moves or resizes on screen; null when it leaves the tree.</summary>
    public event EventHandler<ScreenArea?>? ScreenAreaChanged;

    public IReadOnlyList<GesturePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double ArrowLength
    {
        get => GetValue(ArrowLengthProperty);
        set => SetValue(ArrowLengthProperty, value);
    }

    public Geometry? Geometry
    {
        get => GetValue(GeometryProperty);
        private set => SetValue(GeometryProperty, value);
    }

    /// <summary>This control's bounds in physical screen pixels, or null when it is not on screen.</summary>
    public ScreenArea? CurrentScreenArea()
    {
        if (TopLevel.GetTopLevel(this) is not { } top || !this.IsAttachedToVisualTree() || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return null;
        }

        // The hook's units per DIP: physical pixels on Windows; points on macOS, where the pointer, Avalonia's screen
        // positions and its DIPs are all points (as the trail overlay has it), so a Retina screen's 2× must not apply.
        var scale = OperatingSystem.IsMacOS() ? 1 : top.RenderScaling;
        var origin = this.PointToScreen(new Point(0, 0));
        return new ScreenArea(origin.X, origin.Y, Bounds.Width * scale, Bounds.Height * scale, scale);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty || change.Property == ArrowLengthProperty)
        {
            Show(Points);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PositionChanged += OnMoved;
        }

        LayoutUpdated += OnMoved;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnMoved;
        if (_window is not null)
        {
            _window.PositionChanged -= OnMoved;
            _window = null;
        }

        ScreenAreaChanged?.Invoke(this, null);
    }

    /// <summary>
    /// The pointer always crosses into the area before a stroke is drawn in it, with any button: publishing here keeps the
    /// position current even when the window was placed after the last layout pass (Joel, 2026-10-09: a Redraw window's
    /// stroke-button strokes went to the normal gesture path, the published position being stale).
    /// </summary>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        ScreenAreaChanged?.Invoke(this, CurrentScreenArea());
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDrawing = true;
        _drawing.Clear();
        _drawing.Add(ToPoint(e.GetPosition(this)));
        e.Pointer.Capture(this);
        Show(_drawing);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDrawing)
        {
            return;
        }

        _drawing.Add(ToPoint(e.GetPosition(this)));
        Show(_drawing);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDrawing)
        {
            return;
        }

        _isDrawing = false;
        e.Pointer.Capture(null);
        var stroke = _drawing.ToArray();
        _drawing.Clear();
        StrokeCompleted?.Invoke(this, stroke);
    }

    private static GesturePoint ToPoint(Point position) => new(position.X, position.Y);

    private void OnMoved(object? sender, EventArgs e) => ScreenAreaChanged?.Invoke(this, CurrentScreenArea());

    private void Show(IReadOnlyList<GesturePoint>? points)
    {
        var hasPoints = points is { Count: > 0 };
        PseudoClasses.Set(":empty", !hasPoints);
        Geometry = hasPoints ? GlyphGeometry.BuildRaw(points!, ArrowLength) : null;
    }
}
