using System.Globalization;
using Augram.App.Themes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Augram.App.Inspector;

/// <summary>
/// The F1 layout inspector (ADR-0002 §5d), Debug builds only. Sits on top of the shell in the main
/// window; while F1 is held it outlines every control carrying <see cref="Region.NameProperty"/> in
/// the sibling tree, coloured by nesting depth from the theme's <c>Inspector.Depth*</c> tokens, and a
/// left click copies <see cref="InspectorReport"/> for the innermost region under the pointer.
/// Drawn as one overlay rather than Avalonia adorners so a single pass covers every region.
/// </summary>
public sealed class InspectorOverlay : Control
{
    private const double LabelFontSize = 10;
    private readonly List<(Control Control, int Depth, Rect Bounds)> _regions = [];
    private TopLevel? _topLevel;
    private bool _active;

#if DEBUG
    public static bool IsAvailable => true;
#else
    public static bool IsAvailable => false;
#endif

    public bool IsActive
    {
        get => _active;
        private set
        {
            if (_active == value)
            {
                return;
            }

            _active = value;
            IsHitTestVisible = value;
            if (value)
            {
                CollectRegions();
            }

            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (!IsActive)
        {
            return;
        }

        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        var labelBrush = new SolidColorBrush(Token("Inspector.Label"));
        foreach (var (control, depth, bounds) in _regions)
        {
            var colour = new SolidColorBrush(Token("Inspector.Depth" + (depth % 4).ToString(CultureInfo.InvariantCulture)));
            context.DrawRectangle(null, new Pen(colour, 1), bounds);
            var label = new FormattedText(
                Region.GetName(control) ?? string.Empty,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                LabelFontSize,
                labelBrush);
            var box = new Rect(bounds.X, bounds.Y, label.Width + 4, label.Height + 2);
            context.DrawRectangle(colour, null, box);
            context.DrawText(label, new Point(bounds.X + 2, bounds.Y + 1));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        IsHitTestVisible = false;
        if (!IsAvailable)
        {
            return;
        }

        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topLevel?.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _topLevel?.RemoveHandler(KeyUpEvent, OnKeyUp);
        _topLevel = null;
        IsActive = false;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsActive || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        Control? hit = null;
        var hitDepth = -1;
        foreach (var (control, depth, bounds) in _regions)
        {
            if (bounds.Contains(point) && depth > hitDepth)
            {
                hit = control;
                hitDepth = depth;
            }
        }

        if (hit is not null && _topLevel?.Clipboard is { } clipboard)
        {
            _ = clipboard.SetTextAsync(InspectorReport.Describe(hit, ThemeSelector.Active));
        }

        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            IsActive = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            IsActive = false;
        }
    }

    private Color Token(string key) =>
        this.TryFindResource(key, out var value) && value is Color colour ? colour : Colors.Magenta;

    private void CollectRegions()
    {
        _regions.Clear();
        if (this.GetVisualParent() is not { } parent)
        {
            return;
        }

        foreach (var sibling in parent.GetVisualChildren())
        {
            if (!ReferenceEquals(sibling, this))
            {
                Collect(sibling, 0);
            }
        }
    }

    private void Collect(Visual visual, int depth)
    {
        var next = depth;
        if (visual is Control control && Region.GetName(control) is not null && control.IsEffectivelyVisible
            && control.TransformToVisual(this) is { } transform)
        {
            _regions.Add((control, depth, new Rect(control.Bounds.Size).TransformToAABB(transform)));
            next = depth + 1;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            Collect(child, next);
        }
    }
}
