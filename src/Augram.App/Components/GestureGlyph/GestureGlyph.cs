using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Augram.App.Components.GestureGlyph;

/// <summary>
/// Lookless gesture icon (F4): <see cref="Points"/> in, <see cref="StrokePoints"/> out that the theme's template draws
/// with a <see cref="GlyphStroke"/> named <c>PART_Stroke</c> (dim at the start, full colour at the arrowhead; Joel,
/// 2026-10-08), and the same as a <see cref="Geometry"/> for anything that wants a path. Both are rebuilt only when the
/// points or the size change, by <see cref="GlyphGeometry"/>; <see cref="IsActive"/> false sets the
/// <c>:inactive</c> pseudo-class so the theme can grey it (F3). Works at any size: a 48 px row tile
/// or a 300 px training preview, the theme decides.
/// </summary>
[PseudoClasses(":inactive", ":empty")]
public sealed class GestureGlyph : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> PointsProperty =
        AvaloniaProperty.Register<GestureGlyph, IReadOnlyList<GesturePoint>?>(nameof(Points));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<GestureGlyph, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<double> ArrowLengthProperty =
        AvaloniaProperty.Register<GestureGlyph, double>(nameof(ArrowLength), 8);

    public static readonly StyledProperty<Geometry?> GeometryProperty =
        AvaloniaProperty.Register<GestureGlyph, Geometry?>(nameof(Geometry));

    public static readonly StyledProperty<IReadOnlyList<Point>?> StrokePointsProperty =
        AvaloniaProperty.Register<GestureGlyph, IReadOnlyList<Point>?>(nameof(StrokePoints));

    private IReadOnlyList<GesturePoint>? _builtFor;
    private Size _builtAt;

    public GestureGlyph()
    {
        PseudoClasses.Set(":empty", true);
    }

    /// <summary>Raw points of the sample to draw (the gesture's first sample); null or empty draws nothing.</summary>
    public IReadOnlyList<GesturePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Arrowhead wing length in pixels; the theme may set it per size.</summary>
    public double ArrowLength
    {
        get => GetValue(ArrowLengthProperty);
        set => SetValue(ArrowLengthProperty, value);
    }

    /// <summary>The polyline plus arrowhead in this control's coordinates; the template's <c>Path</c> binds it.</summary>
    public Geometry? Geometry
    {
        get => GetValue(GeometryProperty);
        private set => SetValue(GeometryProperty, value);
    }

    /// <summary>The same stroke as points in this control's coordinates, for the template's <see cref="GlyphStroke"/> (the start-to-end gradient).</summary>
    public IReadOnlyList<Point>? StrokePoints
    {
        get => GetValue(StrokePointsProperty);
        private set => SetValue(StrokePointsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty || change.Property == PaddingProperty || change.Property == ArrowLengthProperty)
        {
            Rebuild(force: true);
        }
        else if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(":inactive", !IsActive);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Rebuild(force: false);
    }

    private void Rebuild(bool force)
    {
        var points = Points;
        var size = Bounds.Size;
        if (!force && ReferenceEquals(points, _builtFor) && size == _builtAt)
        {
            return;
        }

        _builtFor = points;
        _builtAt = size;
        var hasPoints = points is { Count: > 0 };
        PseudoClasses.Set(":empty", !hasPoints);
        var drawable = hasPoints && size.Width > 0 && size.Height > 0;
        Geometry = drawable ? GlyphGeometry.Build(points!, size, Padding, ArrowLength) : null;
        StrokePoints = drawable ? GlyphGeometry.StrokePoints(points!, size, Padding) : null;
    }
}
