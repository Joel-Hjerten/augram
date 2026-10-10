using Augram.Core.Mapping;

namespace Augram.App.Components.Steps;

/// <summary>
/// What a step's form may adapt to about the command the step belongs to (plan 0005): its <see cref="Trigger"/> as the header
/// shows it on this platform (the draft while one waits). The Remap form offers only a key output on a button trigger, since
/// Core refuses the others there (decision 8). Data in, like every component's input; <see cref="None"/> where there is no
/// command (the gallery's bare forms, tests).
/// </summary>
public sealed record StepFormContext(Trigger Trigger)
{
    public static StepFormContext None { get; } = new(Trigger.None);

    /// <summary>The context of a command whose trigger here is <paramref name="trigger"/> (none: <see cref="None"/>).</summary>
    public static StepFormContext For(Trigger? trigger) => trigger is null || trigger == Trigger.None ? None : new(trigger);
}
