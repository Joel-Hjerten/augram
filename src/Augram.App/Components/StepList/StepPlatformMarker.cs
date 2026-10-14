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
/// on, so a Windows "Win+D" never shows as "Cmd+D". A command row shows its own version when it has one ("own macOS
/// version", "· Windows original changed" when that moved on; "has macOS version" where the original runs), else "from
/// Windows", with "· converted" or "· N need a macOS version" when any of its steps do.
/// </summary>
public static class StepPlatformMarker
{
    public static StepRowText For(CommandStep step, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(step);
        var original = step.Step.SummaryOn(step.AuthoredOn);
        if (step.AuthoredOn == here)
        {
            return new StepRowText(original, null);
        }

        var from = $"from {step.AuthoredOn.DisplayName()}";
        var planned = step.ForPlatform(here);
        return planned.Kind switch
        {
            StepConversionKind.Converted => new StepRowText($"{original} → {planned.Step!.SummaryOn(here)}", $"{from} · converted"),
            StepConversionKind.NotConvertible => new StepRowText(original, $"{from} · needs a {here.DisplayName()} version"),
            _ => new StepRowText(original, step.Step.Type.IsPlatformNeutral ? null : from),
        };
    }

    public static string? ForCommand(Command command, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.OwnVersion is { } own)
        {
            var stale = command.IsOwnVersionStale;
            return own.Platform == here
                ? $"own {here.DisplayName()} version" + (stale ? $" · {here.Other().DisplayName()} original changed" : string.Empty)
                : $"has {own.Platform.DisplayName()} version" + (stale ? " · check it there" : string.Empty);
        }

        return ForSteps(command.Steps, here);
    }

    /// <summary>The marker of a step list run as it is: where it came from and what its conversion lacks.</summary>
    public static string? ForSteps(IReadOnlyList<CommandStep> steps, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var elsewhere = steps.Where(step => step.AuthoredOn != here).ToList();
        if (elsewhere.Count == 0)
        {
            return null;
        }

        var marker = $"from {elsewhere[0].AuthoredOn.DisplayName()}";
        var plans = elsewhere.Select(step => step.ForPlatform(here)).ToList();
        var missing = plans.Count(plan => plan.Kind == StepConversionKind.NotConvertible);
        if (missing > 0)
        {
            return $"{marker} · {missing} need{(missing == 1 ? "s" : string.Empty)} a {here.DisplayName()} version";
        }

        return plans.Any(plan => plan.Kind == StepConversionKind.Converted) ? $"{marker} · converted" : marker;
    }

    /// <summary>
    /// The command row's badge (<see cref="PlatformBadge"/>): "Win + Mac" with an own version (wanting a look while the original
    /// changed since), the other platform's name when its steps run here (wanting a look while one has no counterpart here), none
    /// for steps authored here.
    /// </summary>
    public static PlatformBadge? BadgeFor(Command command, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.OwnVersion is not null)
        {
            return new PlatformBadge($"{Short(HostPlatform.Windows)} + {Short(HostPlatform.MacOS)}", command.IsOwnVersionStale);
        }

        var elsewhere = command.Steps.Where(step => step.AuthoredOn != here).ToList();
        return elsewhere.Count == 0
            ? null
            : new PlatformBadge(Short(elsewhere[0].AuthoredOn), elsewhere.Any(step => step.ForPlatform(here).Kind == StepConversionKind.NotConvertible));
    }

    /// <summary>The badge of a command used on one platform only (its "Use on" set, F8): that platform's name; none otherwise.</summary>
    public static PlatformBadge? BadgeFor(PlatformSet set) => set switch
    {
        PlatformSet.Windows => new PlatformBadge(Short(HostPlatform.Windows), false),
        PlatformSet.MacOS => new PlatformBadge(Short(HostPlatform.MacOS), false),
        _ => null,
    };

    /// <summary>A "Use on" set in a few words (F8): "Windows only", "macOS only", "used nowhere", "every platform".</summary>
    public static string Only(PlatformSet set) => set switch
    {
        PlatformSet.None => "used nowhere",
        PlatformSet.Windows => "Windows only",
        PlatformSet.MacOS => "macOS only",
        _ => "every platform",
    };

    private static string Short(HostPlatform platform) => platform == HostPlatform.MacOS ? "Mac" : "Win";
}
