#if DEBUG
using Augram.App.Components.GestureDrawArea;
using Augram.App.Components.GestureGlyph;
using Augram.App.Declarations;
using Augram.App.Import;
using Augram.App.Training;
using Augram.App.ViewModels;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Import.StrokesPlus;

namespace Augram.App.DevGallery;

/// <summary>Gallery pages for the Gestures tab's components (M1 step 7), all over fake or starter data.</summary>
public static class GestureGalleryPages
{
    private static readonly IReadOnlyList<Gesture> Starter = StarterGestures.All();

    public static ScreenDeclaration GlyphPage() =>
        new FormScreen("GestureGlyph",
        [
            new Section("Shapes from the starter set, default tile size",
            [
                new CustomField("Up (straight)", () => Glyph("Up")),
                new CustomField("Circle (loop)", () => Glyph("Circle")),
                new CustomField("S (letter)", () => Glyph("S")),
                new CustomField("L Down Right", () => Glyph("L Down Right")),
                new CustomField("Inactive", () => Glyph("Z", isActive: false)),
                new CustomField("No sample", () => new GestureGlyph()),
            ]),
            new Section("Sizes",
            [
                new CustomField("Tiny (24)", () => Glyph("Right", size: 24, arrow: 4)),
                new CustomField("Row (48)", () => Glyph("Right", size: 48, arrow: 6)),
                new CustomField("Large (200)", () => Glyph("C", size: 200, arrow: 16)),
                new CustomField("Wide tile (200 × 64)", () => Glyph("Up Right", width: 200, height: 64)),
            ]),
        ]);

    public static ScreenDeclaration GridPage()
    {
        var library = new GestureLibrary(Starter.Select((gesture, i) => i % 5 == 4 ? gesture with { IsActive = false } : gesture));
        var vm = new GesturesViewModel(library, () => RecognitionOptions.Default, new GalleryTrainingPresenter(library), new GalleryImportPresenter());
        return Screens.GesturesScreen.Declare(vm);
    }

    public static ScreenDeclaration DrawAreaPage() =>
        new FormScreen("GestureDrawArea",
        [
            new Section("Draw with the left button; the stroke stays where drawn",
            [
                new CustomField("Empty", () => new GestureDrawArea { Width = 320, Height = 200 }),
                new CustomField("With a stroke", () => new GestureDrawArea { Width = 320, Height = 200, Points = Shift(Starter[18].Samples[0], 60, 40) }),
            ]),
        ]);

    public static ScreenDeclaration TrainingPage()
    {
        var session = new TrainingSession(new GestureLibrary(Starter), () => RecognitionOptions.Default);
        session.Begin(TrainingRequest.NewGesture);
        session.ReplaceStroke(Shift(Starter[0].Samples[0], 120, 60));
        return new FormScreen("Training popup content",
        [
            new Section("TrainingView over a live session (Accept adds to a throwaway library)",
            [
                new CustomField("TrainingView", () => new TrainingView { Width = 480, Height = 480 }, new TrainingViewModel(session)),
            ]),
        ]);
    }

    public static ScreenDeclaration ImportPage()
    {
        var library = new GestureLibrary(Starter);
        var vm = new ImportViewModel(library, NullEventLog.Instance);
        var theirs = new List<Gesture>
        {
            Starter[0] with { Id = GestureId.New(), Samples = [Starter[0].Samples[0], Starter[0].Samples[0]] },
            Starter[3] with { Id = GestureId.New(), IsActive = false },
            new(GestureId.New(), "Synthetic W", IsActive: true, [Starter[18].Samples[0]]),
        };
        vm.Load(new ImportResult(theirs, [new ImportWarning(ImportSeverity.Warning, "Synthetic Empty", "no usable sample; skipped")], new SourceStats(4, 5, 212, 19)));
        return new FormScreen("Import dialog content",
        [
            new Section("ImportView over a plan with two conflicts and one addition",
            [
                new CustomField("ImportView", () => new ImportView { Width = 600, Height = 420 }, vm),
            ]),
        ]);
    }

    private static GestureGlyph Glyph(string name, bool isActive = true, double size = 64, double? width = null, double? height = null, double arrow = 8)
        => new()
        {
            Points = Starter.Single(gesture => gesture.Name == name).Samples[0],
            IsActive = isActive,
            Width = width ?? size,
            Height = height ?? size,
            ArrowLength = arrow,
        };

    private static GesturePoint[] Shift(IReadOnlyList<GesturePoint> points, double dx, double dy)
        => points.Select(point => new GesturePoint(point.X + dx, point.Y + dy)).ToArray();

    private sealed class GalleryTrainingPresenter : ITrainingPresenter
    {
        private readonly GestureLibrary _library;

        public GalleryTrainingPresenter(GestureLibrary library)
        {
            _library = library;
        }

        /// <summary>No window in the gallery: adds a copy of Z so the grid visibly reacts.</summary>
        public void Open(TrainingRequest request)
        {
            var n = _library.All.Count;
            _library.Add(new Gesture(GestureId.New(), "Gallery gesture " + n, IsActive: true, [Starter[18].Samples[0]]));
        }
    }

    private sealed class GalleryImportPresenter : IImportPresenter
    {
        public Task OpenAsync() => Task.CompletedTask;
    }
}
#endif
