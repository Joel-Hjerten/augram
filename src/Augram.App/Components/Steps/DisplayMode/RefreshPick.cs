using Augram.Core.Abstractions;
using Augram.Core.Steps.DisplayMode;

namespace Augram.App.Components.Steps.DisplayMode;

/// <summary>
/// One entry of the Display mode form's Refresh dropdown: Auto (keep current), Highest available, or one exact rate. The
/// step stores the same three as <see cref="DisplayModeStep.Refresh"/> and <see cref="DisplayModeStep.HighestRefresh"/>.
/// </summary>
public sealed record RefreshPick(RefreshRate? Rate, bool Highest)
{
    public static RefreshPick Auto { get; } = new(null, false);

    public static RefreshPick HighestAvailable { get; } = new(null, true);

    public static RefreshPick Of(DisplayModeStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.HighestRefresh ? HighestAvailable : new RefreshPick(step.Refresh, false);
    }
}
