using Augram.Core.Capture;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>The one place a <see cref="Trigger"/> becomes a <see cref="TriggerKind"/> and a kind gets its label; the dropdown and the row badge use nothing else.</summary>
public static class TriggerKindExtensions
{
    public static IReadOnlyList<TriggerKind> All { get; } = Enum.GetValues<TriggerKind>();

    public static string Label(this TriggerKind kind) => kind switch
    {
        TriggerKind.Gesture => "Gesture",
        TriggerKind.WheelUp => "Wheel up",
        TriggerKind.WheelDown => "Wheel down",
        _ => "No trigger",
    };

    public static TriggerKind KindOf(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger switch
        {
            Trigger.GestureTrigger => TriggerKind.Gesture,
            Trigger.WheelTrigger wheel => wheel.Direction == WheelDirection.Up ? TriggerKind.WheelUp : TriggerKind.WheelDown,
            _ => TriggerKind.None,
        };
    }
}
