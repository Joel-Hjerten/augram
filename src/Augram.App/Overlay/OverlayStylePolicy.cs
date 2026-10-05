using Augram.Core.Abstractions;

namespace Augram.App.Overlay;

/// <summary>
/// The one rule that decides whether the trail window may be on screen: it must be click-through, or
/// a topmost full-screen window swallows every mouse click on the machine. A window that is click-through
/// but still activatable or taskbar-visible is a cosmetic fault, shown and logged as a warning.
/// </summary>
public static class OverlayStylePolicy
{
    public static OverlayStyleDecision Decide(OverlayStyleReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.ClickThrough ? OverlayStyleDecision.Show : OverlayStyleDecision.Hide;
    }

    /// <summary>True when the window may show but not every wanted property held; the caller logs a warning.</summary>
    public static bool HasCosmeticFault(OverlayStyleReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.ClickThrough && (!report.NoActivate || !report.ToolWindow);
    }
}
