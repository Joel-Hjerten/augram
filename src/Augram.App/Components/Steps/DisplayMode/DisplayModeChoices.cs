using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps.DisplayMode;

namespace Augram.App.Components.Steps.DisplayMode;

/// <summary>
/// The Display mode form's dropdown lists, from what the connected displays offer now: "Auto (keep current)" first,
/// then every resolution any display offers, largest first, or every rate offered at the chosen resolution (all
/// rates when the resolution is Auto), highest first, shown as "119.88 Hz". The step's own value is always listed,
/// offered now or not (a display that is off, a config from the other machine), so the dropdown never loses it.
/// </summary>
public static class DisplayModeChoices
{
    public const string AutoText = "Auto (keep current)";

    public static IReadOnlyList<Choice<DisplayResolution?>> Resolutions(IReadOnlyList<DisplayInfo> displays, DisplayResolution? stored)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var sizes = displays.SelectMany(display => display.Modes).Select(mode => mode.Resolution);
        if (stored is { } own)
        {
            sizes = sizes.Append(own);
        }

        return [new Choice<DisplayResolution?>(AutoText, null), .. DisplayModeResolver.LargestFirst(sizes).Select(size => new Choice<DisplayResolution?>(size.ToString(), size))];
    }

    public static IReadOnlyList<Choice<RefreshRate?>> Rates(IReadOnlyList<DisplayInfo> displays, DisplayResolution? size, RefreshRate? stored)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var rates = displays.SelectMany(display => display.Modes)
            .Where(mode => size is null || mode.Resolution == size)
            .Select(mode => mode.Refresh);
        if (stored is { } own)
        {
            rates = rates.Append(own);
        }

        return [new Choice<RefreshRate?>(AutoText, null), .. DisplayModeResolver.HighestFirst(rates).Select(rate => new Choice<RefreshRate?>(rate.ToString(), rate))];
    }

    public static IReadOnlyList<Choice<DisplayTarget>> Targets { get; } =
    [
        new("Display under the gesture", DisplayTarget.UnderGesture),
        new("Main display", DisplayTarget.Main),
    ];
}
