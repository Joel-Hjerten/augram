using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.DisplayMode;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.DisplayMode;
using Augram.Core.Steps.Hdr;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Steps;

/// <summary>The Display mode and HDR forms: the lists come from the displays the adapter reports, Auto first, and every pick emits one step.</summary>
public sealed class DisplayStepFormTests
{
    private static readonly DisplayResolution Uhd = new(3840, 2160);
    private static readonly DisplayResolution FullHd = new(1920, 1080);

    [Fact]
    public void ResolutionsAreAutoThenTheUnionLargestFirst_AndTheStoredOneIsKept()
    {
        var choices = DisplayModeChoices.Resolutions(TwoDisplays.Instance.Displays(), new DisplayResolution(5120, 1440));

        Assert.Equal(["Auto (keep current)", "3840×2160", "5120×1440", "2560×1440", "1920×1080"], choices.Select(choice => choice.Label));
        Assert.Null(choices[0].Value);
    }

    [Fact]
    public void RatesAreThoseAtTheChosenResolutionHighestFirst_OrAllForAuto()
    {
        var displays = TwoDisplays.Instance.Displays();

        Assert.Equal(["Auto (keep current)", "60 Hz", "59.94 Hz", "24 Hz", "23.976 Hz"], DisplayModeChoices.Rates(displays, Uhd, null).Select(choice => choice.Label));
        Assert.Equal(["Auto (keep current)", "144 Hz", "120 Hz", "60 Hz", "59.94 Hz", "24 Hz", "23.976 Hz"], DisplayModeChoices.Rates(displays, null, null).Select(choice => choice.Label));
        Assert.Contains("50 Hz", DisplayModeChoices.Rates(displays, Uhd, RefreshRate.FromHertz(50)).Select(choice => choice.Label));
    }

    [AvaloniaFact]
    public void TheFormShowsTheStepAndEveryPickEmitsOneStep()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new DisplayModeStepForm(TwoDisplays.Instance), new DisplayModeStep(Uhd, RefreshRate.FromHertz(23.976)), changes.Add);

        Assert.Equal(["Resolution", "Refresh", "Display"], form.GetVisualDescendants().OfType<FieldRow>().Select(row => row.Label));
        Assert.Equal(["3840×2160", "23.976 Hz", "Display under the gesture"], Combos(form).Select(combo => combo.SelectedItem));

        Combos(form)[2].SelectedItem = "Main display";
        Assert.Equal(new DisplayModeStep(Uhd, RefreshRate.FromHertz(23.976), DisplayTarget.Main), Assert.Single(changes));

        Combos(form)[1].SelectedItem = "Auto (keep current)";
        Assert.Equal(new DisplayModeStep(Uhd, null, DisplayTarget.Main), changes[^1]);
    }

    [AvaloniaFact]
    public void ChoosingAResolutionReListsTheRatesAndKeepsTheChosenRate()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new DisplayModeStepForm(TwoDisplays.Instance), new DisplayModeStep(Uhd, RefreshRate.FromHertz(24)), changes.Add);

        Combos(form)[0].SelectedItem = "1920×1080";

        Assert.Equal(new DisplayModeStep(FullHd, RefreshRate.FromHertz(24)), Assert.Single(changes));
        form.UpdateLayout();
        var rates = Combos(form)[1];
        Assert.Equal("24 Hz", rates.SelectedItem);
        Assert.Contains("144 Hz", rates.Items.Cast<object>().Select(item => item.ToString()));
    }

    [AvaloniaFact]
    public void WithoutAnAdapterTheFormStillShowsAutoAndTheStepsOwnValues()
    {
        var (form, _) = Show(new DisplayModeStepForm(), new DisplayModeStep(FullHd, RefreshRate.FromHertz(119.88)), _ => { });

        Assert.Equal(["Auto (keep current)", "1920×1080"], Combos(form)[0].Items.Cast<object>().Select(item => item.ToString()));
        Assert.Equal("119.88 Hz", Combos(form)[1].SelectedItem);
    }

    [AvaloniaFact]
    public void TheRegistryBuildsBothDisplayForms()
    {
        Assert.True(StepFormRegistry.Default.Supports(DisplayModeStepType.Instance));
        Assert.True(StepFormRegistry.Default.Supports(HdrStepType.Instance));
    }

    [AvaloniaFact]
    public void TheHdrFormPicksAnActionAndATarget()
    {
        var changes = new List<IStep>();
        var form = StepFormRegistry.Default.Build(new HdrStep(), changes.Add);
        var window = new Window { Content = form, Width = 600, Height = 400 };
        window.Show();

        Assert.Equal(["Toggle HDR", "Display under the gesture"], Combos(form).Select(combo => combo.SelectedItem));
        Assert.Equal(["Toggle HDR", "HDR on", "HDR off"], Combos(form)[0].Items.Cast<object>().Select(item => item.ToString()));

        Combos(form)[0].SelectedItem = "HDR off";
        Combos(form)[1].SelectedItem = "Main display";

        Assert.Equal([new HdrStep(HdrAction.Off), new HdrStep(HdrAction.Off, DisplayTarget.Main)], changes);
    }

    private static List<ComboBox> Combos(Control form) => [.. form.GetVisualDescendants().OfType<ComboBox>()];

    private static (Control Form, Window Window) Show(IStepForm form, IStep step, Action<IStep> changed)
    {
        var control = form.Build(step, changed);
        var window = new Window { Content = control, Width = 600, Height = 400 };
        window.Show();
        return (control, window);
    }

    /// <summary>A 4K TV (24, 60 and their TV twins) and a 1440p monitor at 144 Hz; changes nothing.</summary>
    private sealed class TwoDisplays : IDisplayModes
    {
        public static TwoDisplays Instance { get; } = new();

        public HostPlatform Platform => HostPlatform.Windows;

        public bool CanSwitchHdr => true;

        public IReadOnlyList<DisplayInfo> Displays() =>
        [
            new("tv", "TV", new DisplayBounds(0, 0, 3840, 2160), true, Mode(Uhd, 60), [.. new[] { Uhd, FullHd }.SelectMany(size => new[] { 23, 24, 59, 60 }.Select(rate => Mode(size, rate)))]),
            new("monitor", "Monitor", new DisplayBounds(3840, 0, 2560, 1440), false, Mode(new(2560, 1440), 144), [Mode(new(2560, 1440), 144), Mode(new(2560, 1440), 120), Mode(FullHd, 144)]),
        ];

        public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode) => throw new InvalidOperationException("forms never change a display");

        public DisplayChangeResult SetHdr(DisplayInfo display, bool on) => throw new InvalidOperationException("forms never change a display");

        private static VideoMode Mode(DisplayResolution size, int legacyRate) => new(size, RefreshRate.FromLegacyHertz(legacyRate));
    }
}
