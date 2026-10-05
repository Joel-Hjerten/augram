using Augram.Core.Capture;
using Augram.Core.Config;
using Xunit;

namespace Augram.Core.Tests.Config;

public sealed class SettingsStoreTests
{
    [Fact]
    public void SettersReplaceOneSectionAndRaiseChanged()
    {
        var store = new SettingsStore(Settings.Default);
        int raised = 0;
        store.Changed += (_, _) => raised++;

        var result = store.SetGeneral(GeneralSettings.Default with { StrokeButton = MouseButton.Middle });

        Assert.Same(result, store.Current);
        Assert.Equal(MouseButton.Middle, store.Current.General.StrokeButton);
        Assert.Equal(Settings.Default.Trail, store.Current.Trail);
        Assert.Equal(1, raised);
        Assert.Equal(1, store.Version);
    }

    [Fact]
    public void UndoAndRedoWalkTheHistory()
    {
        var store = new SettingsStore(Settings.Default);
        store.SetTrail(new TrailSettings { WidthPx = 2 });
        store.SetTrail(new TrailSettings { WidthPx = 3 });

        Assert.True(store.Undo());
        Assert.Equal(2, store.Current.Trail.WidthPx);
        Assert.True(store.Undo());
        Assert.Equal(TrailSettings.Default, store.Current.Trail);
        Assert.False(store.Undo());
        Assert.True(store.Redo());
        Assert.Equal(2, store.Current.Trail.WidthPx);

        store.SetNoMatch(NoMatchBehaviour.ReplayClick);

        Assert.False(store.CanRedo);
        Assert.Equal(2, store.Current.Trail.WidthPx);
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(5, 1.5)]
    [InlineData(5, -0.1)]
    [InlineData(101, 0.5)]
    public void OutOfRangeTrailValuesAreRejectedAndNotCommitted(double width, double opacity)
    {
        var store = new SettingsStore(Settings.Default);

        Assert.Throws<SettingsValidationException>(() => store.SetTrail(new TrailSettings { WidthPx = width, Opacity = opacity }));

        Assert.Equal(TrailSettings.Default, store.Current.Trail);
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void OutOfRangeCaptureAndRecognitionValuesAreRejected()
    {
        var store = new SettingsStore(Settings.Default);

        Assert.Throws<SettingsValidationException>(() => store.SetCapture(new CaptureThresholds(StartDistancePx: 0)));
        Assert.Throws<SettingsValidationException>(() => store.SetCapture(new CaptureThresholds(CancelDelayMs: -1)));
        Assert.Throws<SettingsValidationException>(() => store.SetRecognition(new Core.Recognition.RecognitionOptions(Precision: 1)));
        Assert.Throws<SettingsValidationException>(() => store.SetRecognition(new Core.Recognition.RecognitionOptions(Threshold: 101)));
        Assert.Throws<SettingsValidationException>(() => new SettingsStore(Settings.Default with { Trail = new TrailSettings { Opacity = 2 } }));
    }
}
