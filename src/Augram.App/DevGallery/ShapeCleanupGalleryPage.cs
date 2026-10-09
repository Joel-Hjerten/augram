#if DEBUG
using Augram.App.Components.GestureDrawArea;
using Augram.App.Components.GestureGlyph;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Gestures.Cleanup;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.DevGallery;

/// <summary>
/// Shape cleanup preview (plan 0001 M2 step 10, learnings 0004), before anything is stored: a draw area whose stroke is
/// shown with its cleaned shape over it in the cleaned colour, the pieces named; and every gesture of this machine's
/// configuration side by side, as drawn and cleaned. Reads the configuration file only; nothing is written.
/// </summary>
public static class ShapeCleanupGalleryPage
{
    private const double GlyphSize = 72;

    public static ScreenDeclaration Page()
    {
        var verdict = "Draw with the left button.";
        var verdictBinding = new DelegateBinding<string>(() => verdict, propertyName: null);
        return new FormScreen("Shape cleanup",
        [
            new Section("Draw a stroke: the cleaned shape shows over it in gold",
            [
                new CustomField("Draw here", () => DrawAndClean(text =>
                {
                    verdict = text;
                    verdictBinding.NotifyChanged();
                })),
                new NoteField("Pieces", verdictBinding),
            ]),
            new Section("This machine's gestures: as drawn, then cleaned", [.. Gestures().Select(Row)]),
        ]);
    }

    private static Control DrawAndClean(Action<string> verdict)
    {
        var drawn = new GestureDrawArea { Width = 520, Height = 360 };
        var cleaned = new GestureDrawArea { Width = 520, Height = 360, IsHitTestVisible = false };
        cleaned.Classes.Add("cleaned");
        drawn.StrokeCompleted += (_, stroke) =>
        {
            var shape = ShapeCleanup.Clean(stroke);
            drawn.Points = stroke;
            cleaned.Points = shape.Points;
            verdict($"{string.Join(", ", shape.Pieces)} · {shape.Corners.Count} corner(s) · {stroke.Count} → {shape.Points.Count} points");
        };
        return new Panel { Children = { drawn, cleaned } };
    }

    private static Field Row(Gesture gesture)
    {
        var sample = gesture.Samples[0];
        var shape = ShapeCleanup.Clean(sample);
        return new CustomField($"{gesture.Name} ({string.Join(" ", shape.Pieces)})", () => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                new GestureGlyph { Points = sample, Width = GlyphSize, Height = GlyphSize },
                new GestureGlyph { Points = shape.Points, Width = GlyphSize, Height = GlyphSize },
            },
        });
    }

    private static IEnumerable<Gesture> Gestures()
    {
        var file = Path.Combine(AppPaths.ConfigFolder, "augram.json");
        try
        {
            return File.Exists(file) ? ConfigSerializer.Read(File.ReadAllText(file)).Gestures.Where(gesture => gesture.Samples.Count > 0) : StarterGestures.All();
        }
        catch (Exception exception) when (exception is IOException or ConfigFormatException or UnauthorizedAccessException)
        {
            return StarterGestures.All();
        }
    }
}
#endif
