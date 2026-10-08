using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Augram.App.Components.GestureGlyph;

/// <summary>
/// Draws a gesture glyph's polyline with a gradient from <see cref="StartBrush"/> at the first point to <see cref="Stroke"/>
/// at the last, and the arrowhead in <see cref="Stroke"/> (Joel, 2026-10-08): where a stroke starts and which way it runs
/// reads along its whole length, not only at the tip, which helps loops and self-crossing shapes. Gesture pictures only,
/// never the live trail. A brush cannot follow a path, so each segment gets its own colour by arc length
/// (<see cref="GlyphGeometry.Progress"/>), in <see cref="Steps"/> shades so a render makes a handful of pens, not one per
/// segment. The theme sets both brushes (tokens); a non-solid brush draws the whole stroke in <see cref="Stroke"/>.
/// </summary>
public sealed class GlyphStroke : Control
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> PointsProperty =
        AvaloniaProperty.Register<GlyphStroke, IReadOnlyList<Point>?>(nameof(Points));

    public static readonly StyledProperty<IBrush?> StartBrushProperty =
        AvaloniaProperty.Register<GlyphStroke, IBrush?>(nameof(StartBrush));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<GlyphStroke, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<GlyphStroke, double>(nameof(StrokeThickness), 2);

    public static readonly StyledProperty<double> ArrowLengthProperty =
        AvaloniaProperty.Register<GlyphStroke, double>(nameof(ArrowLength), 8);

    /// <summary>Shades between start and end colour; enough that no step is visible at glyph sizes.</summary>
    public const int Steps = 16;

    static GlyphStroke()
    {
        AffectsRender<GlyphStroke>(PointsProperty, StartBrushProperty, StrokeProperty, StrokeThicknessProperty, ArrowLengthProperty);
    }

    /// <summary>The stroke in this control's coordinates, repeats dropped (<see cref="GlyphGeometry.StrokePoints"/>).</summary>
    public IReadOnlyList<Point>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>The colour at the stroke's first point.</summary>
    public IBrush? StartBrush
    {
        get => GetValue(StartBrushProperty);
        set => SetValue(StartBrushProperty, value);
    }

    /// <summary>The colour at the last point and of the arrowhead.</summary>
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double ArrowLength
    {
        get => GetValue(ArrowLengthProperty);
        set => SetValue(ArrowLengthProperty, value);
    }

    /// <summary>The colour at <paramref name="progress"/> (0–1) along the stroke, snapped to one of <see cref="Steps"/> shades.</summary>
    public static Color ShadeAt(Color start, Color end, double progress)
    {
        var t = Math.Round(Math.Clamp(progress, 0, 1) * (Steps - 1)) / (Steps - 1);
        return Color.FromArgb(Mix(start.A, end.A, t), Mix(start.R, end.R, t), Mix(start.G, end.G, t), Mix(start.B, end.B, t));
    }

    public override void Render(DrawingContext context)
    {
        var points = Points;
        if (points is not { Count: > 0 } || Stroke is not { } stroke)
        {
            return;
        }

        var endPen = Pen(stroke);
        if (points.Count == 1)
        {
            // A dot: a zero-length segment with round caps renders as a point.
            context.DrawLine(endPen, points[0], points[0]);
            return;
        }

        if (StartBrush is ISolidColorBrush { Color: var start } && stroke is ISolidColorBrush { Color: var end })
        {
            var progress = GlyphGeometry.Progress(points);
            var pens = new Dictionary<Color, IPen>();
            for (var i = 1; i < points.Count; i++)
            {
                var shade = ShadeAt(start, end, (progress[i - 1] + progress[i]) / 2);
                if (!pens.TryGetValue(shade, out var pen))
                {
                    pen = Pen(new ImmutableSolidColorBrush(shade));
                    pens.Add(shade, pen);
                }

                context.DrawLine(pen, points[i - 1], points[i]);
            }
        }
        else
        {
            for (var i = 1; i < points.Count; i++)
            {
                context.DrawLine(endPen, points[i - 1], points[i]);
            }
        }

        var tip = points[^1];
        var (left, right) = GlyphGeometry.ArrowWings(points[^2], tip, ArrowLength);
        context.DrawLine(endPen, left, tip);
        context.DrawLine(endPen, right, tip);
    }

    private static byte Mix(byte from, byte to, double t) => (byte)Math.Round(from + ((to - from) * t));

    private ImmutablePen Pen(IBrush brush) =>
        new(brush.ToImmutable(), StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
}
