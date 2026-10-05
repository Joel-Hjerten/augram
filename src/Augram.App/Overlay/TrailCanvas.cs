using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Augram.App.Overlay;

/// <summary>
/// Draws the stroke as one polyline with round caps and joins. The geometry is rebuilt per render,
/// which the spike measured at 0.01–0.13 ms for a few hundred points (learnings 0001). The pen is set
/// per stroke from the trail settings; this control owns no colour of its own.
/// </summary>
public sealed class TrailCanvas : Control
{
    private readonly List<Point> _points = new(1024);
    private IPen _pen = new Pen(Brushes.Transparent);

    /// <summary>Raised at the end of every <see cref="Render"/> with the number of points drawn.</summary>
    public event Action<int>? Rendered;

    public int PointCount => _points.Count;

    public void SetPen(IPen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);
        _pen = pen;
    }

    public void SetPoints(IEnumerable<Point> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points.Clear();
        _points.AddRange(points);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_points.Count >= 2)
        {
            var geometry = new StreamGeometry();
            using (var figure = geometry.Open())
            {
                figure.BeginFigure(_points[0], false);
                for (var i = 1; i < _points.Count; i++)
                {
                    figure.LineTo(_points[i]);
                }

                figure.EndFigure(false);
            }

            context.DrawGeometry(null, _pen, geometry);
        }

        Rendered?.Invoke(_points.Count);
    }
}
