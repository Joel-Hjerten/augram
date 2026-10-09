using Augram.Core.Abstractions;

namespace Augram.Core.Steps.DisplayMode;

/// <summary>
/// The decision table of learnings 0002 §4: a step's resolution and refresh targets (null = Auto, keep the current
/// one) against what one display offers now. The size is the target or the current one and must be offered. A refresh
/// target matches exactly first, then the closest rate within 0.2 % (<see cref="RefreshRate.IsNear"/>: a stored 120
/// runs at 119.88 on a display that only has that, and the reverse). Auto keeps the current rate when the new size has
/// it (exact, then near), else takes the offered rate closest to it, the lower on a tie; with no known current rate,
/// the highest. "Highest available" takes the highest rate offered at the size. Anything the display lacks is unsupported with a reason that lists what it has. Pure; the whole mode
/// is decided before anything is applied, so a step never half-changes a display.
/// </summary>
public static class DisplayModeResolver
{
    /// <summary>How many resolutions a reason names before "and N more".</summary>
    public const int ListedResolutions = 8;

    /// <summary>The mode <paramref name="display"/> should switch to, or why it cannot.</summary>
    /// <param name="display">The display the step targets, as listed now.</param>
    /// <param name="resolution">The target size; null keeps the current one.</param>
    /// <param name="refresh">The target rate; null keeps the current one (or the closest the size has).</param>
    /// <param name="highest">The step's "highest available" refresh: the highest known rate offered at the size (<paramref name="refresh"/> is ignored).</param>
    public static DisplayModeResolution Resolve(DisplayInfo display, DisplayResolution? resolution, RefreshRate? refresh, bool highest = false)
    {
        ArgumentNullException.ThrowIfNull(display);
        var size = resolution ?? display.Current.Resolution;
        var rates = display.Modes.Where(mode => mode.Resolution == size).Select(mode => mode.Refresh).Distinct().ToList();
        if (rates.Count == 0)
        {
            return DisplayModeResolution.Unsupported($"{display.Name} has no {size}; it offers {Resolutions(display.Modes)}");
        }

        var rate = highest ? Highest(rates) : refresh is { } wanted ? Matching(rates, wanted) : KeepCurrent(rates, display.Current.Refresh);
        if (rate is not { } chosen)
        {
            return DisplayModeResolution.Unsupported($"{display.Name} has no {refresh} at {size}; {Rates(rates)}");
        }

        var mode = new VideoMode(size, chosen);
        return mode == display.Current ? DisplayModeResolution.Current(mode) : DisplayModeResolution.Change(mode);
    }

    /// <summary>Distinct resolutions, largest first (by area, then width): the order reasons and the step form list them in.</summary>
    public static IReadOnlyList<DisplayResolution> LargestFirst(IEnumerable<DisplayResolution> resolutions)
        => [.. resolutions.Distinct().OrderByDescending(size => size.Area).ThenByDescending(size => size.Width)];

    /// <summary>Distinct known rates, highest first.</summary>
    public static IReadOnlyList<RefreshRate> HighestFirst(IEnumerable<RefreshRate> rates)
        => [.. rates.Where(rate => rate.IsKnown).Distinct().OrderByDescending(rate => rate.Millihertz)];

    /// <summary>The exact rate, else the closest one within <see cref="RefreshRate.IsNear"/>; null when neither is offered.</summary>
    private static RefreshRate? Matching(List<RefreshRate> rates, RefreshRate wanted)
    {
        if (rates.Contains(wanted))
        {
            return wanted;
        }

        var near = rates.Where(wanted.IsNear).OrderBy(rate => Distance(rate, wanted)).ToList();
        return near.Count > 0 ? near[0] : null;
    }

    /// <summary>The highest known rate; the display's only (unknown) rate when it reports none.</summary>
    private static RefreshRate Highest(List<RefreshRate> rates)
        => rates.Where(rate => rate.IsKnown).DefaultIfEmpty(rates[0]).MaxBy(rate => rate.Millihertz);

    private static RefreshRate KeepCurrent(List<RefreshRate> rates, RefreshRate current)
    {
        var known = rates.Where(rate => rate.IsKnown).ToList();
        if (known.Count == 0)
        {
            return rates[0];
        }

        if (!current.IsKnown)
        {
            return known.MaxBy(rate => rate.Millihertz);
        }

        return Matching(known, current)
            ?? known.OrderBy(rate => Distance(rate, current)).ThenBy(rate => rate.Millihertz).First();
    }

    private static int Distance(RefreshRate rate, RefreshRate other) => Math.Abs(rate.Millihertz - other.Millihertz);

    private static string Resolutions(IEnumerable<VideoMode> modes)
    {
        var sizes = LargestFirst(modes.Select(mode => mode.Resolution));
        if (sizes.Count == 0)
        {
            return "no modes at all";
        }

        var listed = string.Join(", ", sizes.Take(ListedResolutions));
        return sizes.Count > ListedResolutions ? $"{listed} and {sizes.Count - ListedResolutions} more" : listed;
    }

    private static string Rates(IEnumerable<RefreshRate> rates)
    {
        var known = HighestFirst(rates);
        return known.Count == 0
            ? "it reports no refresh rates there"
            : $"it offers {string.Join(", ", known.Select(rate => rate.Text))} Hz";
    }
}
