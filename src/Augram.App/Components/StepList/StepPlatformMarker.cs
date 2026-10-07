using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.App.Components.StepList;

/// <summary>
/// The F8 text of step and command rows on the platform Augram runs on (Joel, 2026-10-07: where a step comes from and
/// what runs here must be visible). A step authored here reads as itself. One authored on the other platform reads as
/// its conversion ("Ctrl+W → Cmd+W", marker "from Windows · converted"); as itself with "from Windows · needs a macOS
/// version" when there is no guess; or as itself with "from Windows" when it runs unchanged (a platform-neutral step
/// shows no marker: nothing about it differs). The original always reads in the words of the platform it was authored
/// on, so a Windows "Win+D" never shows as "Cmd+D". A per-step own version reads "own macOS version". A command row
/// shows "from Windows", with "· converted" or "· N need a macOS version" when any of its steps do.
/// </summary>
public static class StepPlatformMarker
{
    public static StepRowText For(CommandStep step, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(step);
        var original = step.Step.SummaryOn(step.AuthoredOn);
        if (step.AuthoredOn == here)
        {
            var elsewhere = Other(here);
            return new StepRowText(original, step.OverrideFor(elsewhere) is null ? null : $"has {Name(elsewhere)} version");
        }

        if (step.OverrideFor(here) is { } own)
        {
            return new StepRowText(own.SummaryOn(here), $"own {Name(here)} version");
        }

        var from = $"from {Name(step.AuthoredOn)}";
        var planned = step.ForPlatform(here);
        return planned.Kind switch
        {
            StepConversionKind.Converted => new StepRowText($"{original} → {planned.Step!.SummaryOn(here)}", $"{from} · converted"),
            StepConversionKind.NotConvertible => new StepRowText(original, $"{from} · needs a {Name(here)} version"),
            _ => new StepRowText(original, step.Step.Type.IsPlatformNeutral ? null : from),
        };
    }

    public static string? ForCommand(IReadOnlyList<CommandStep> steps, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var elsewhere = steps.Where(step => step.AuthoredOn != here).ToList();
        if (elsewhere.Count == 0)
        {
            return steps.Any(step => step.OverrideFor(Other(here)) is not null) ? $"has {Name(Other(here))} version" : null;
        }

        var marker = $"from {Name(elsewhere[0].AuthoredOn)}";
        var plans = elsewhere.Select(step => step.ForPlatform(here)).ToList();
        var missing = plans.Count(plan => plan.Kind == StepConversionKind.NotConvertible);
        if (missing > 0)
        {
            return $"{marker} · {missing} need{(missing == 1 ? "s" : string.Empty)} a {Name(here)} version";
        }

        return plans.Any(plan => plan.Kind == StepConversionKind.Converted) ? $"{marker} · converted" : marker;
    }

    public static string Name(HostPlatform platform) => platform == HostPlatform.MacOS ? "macOS" : "Windows";

    private static HostPlatform Other(HostPlatform platform) => platform == HostPlatform.MacOS ? HostPlatform.Windows : HostPlatform.MacOS;
}
