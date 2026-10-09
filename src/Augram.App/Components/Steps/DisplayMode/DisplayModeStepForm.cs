using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.DisplayMode;
using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.DisplayMode;

/// <summary>
/// The Display mode step's form: Resolution and Refresh dropdowns over what the connected displays offer
/// (<see cref="DisplayModeChoices"/>, read once when the form is built), each with "Auto (keep current)" first, and the
/// target display. Choosing a resolution re-declares the form so Refresh lists that resolution's rates; a chosen rate
/// stays chosen even when the new resolution lacks it (the step resolves it when it runs). The displays come from the
/// application resource <see cref="DisplayModesResourceKey"/>, which <c>EngineModule</c> publishes because forms are
/// built by scan without services; without it (tests, the gallery) only Auto and the step's own values are listed.
/// </summary>
public sealed class DisplayModeStepForm : IStepForm
{
    public const string DisplayModesResourceKey = "Augram.DisplayModes";

    private readonly IDisplayModes? _displays;

    public DisplayModeStepForm()
    {
    }

    /// <summary>A form over explicit displays (tests, the gallery) instead of the published adapter.</summary>
    public DisplayModeStepForm(IDisplayModes displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        _displays = displays;
    }

    public string TypeKey => DisplayModeStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var step = StepParameters.Expect<DisplayModeStep>(current, DisplayModeStepType.Instance);
        var host = new SectionForm.SectionForm();
        Show(host, step, Displays(), changed);
        return host;
    }

    private static void Show(SectionForm.SectionForm host, DisplayModeStep step, IReadOnlyList<DisplayInfo> displays, Action<IStep> changed)
    {
        var state = step;
        void Emit(DisplayModeStep next)
        {
            var reshaped = next.Resolution != state.Resolution;
            state = next;
            changed(next);
            if (reshaped)
            {
                Show(host, next, displays, changed);
            }
        }

        host.Screen = new FormScreen("Display mode",
        [
            new Section("Display mode",
            [
                new DropdownField<DisplayResolution?>(
                    "Resolution",
                    DisplayModeChoices.Resolutions(displays, step.Resolution),
                    new DelegateBinding<DisplayResolution?>(() => state.Resolution, size => Emit(state with { Resolution = size })),
                    "Auto keeps the current one. Pixels on Windows, the \"looks like\" size on macOS."),
                new DropdownField<RefreshPick>(
                    "Refresh",
                    DisplayModeChoices.Rates(displays, step.Resolution, step.Refresh),
                    new DelegateBinding<RefreshPick>(() => RefreshPick.Of(state), pick => Emit(state with { Refresh = pick.Rate, HighestRefresh = pick.Highest })),
                    "Auto keeps the current rate, or the closest the new resolution has. Highest available takes the top rate at that resolution. 120 Hz also matches 119.88 Hz on a display that has only that, and the reverse."),
                new DropdownField<DisplayTarget>(
                    "Display",
                    DisplayModeChoices.Targets,
                    new DelegateBinding<DisplayTarget>(() => state.Target, target => Emit(state with { Target = target })),
                    "With two displays: the one the gesture was drawn on, or always the main one."),
            ]),
        ]);
    }

    private IReadOnlyList<DisplayInfo> Displays()
    {
        var displays = _displays
            ?? (Application.Current?.Resources.TryGetValue(DisplayModesResourceKey, out var found) == true ? found as IDisplayModes : null);
        return displays?.Displays() ?? [];
    }
}
