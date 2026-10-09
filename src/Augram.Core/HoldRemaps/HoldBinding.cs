using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// One command under a hold remap as it runs on one platform (<see cref="HoldRemapPlan"/>): its <see cref="Input"/> (the
/// trigger for this platform) and what the input does, resolved from the steps this platform runs (<see cref="Command.PlanFor"/>,
/// so a platform version's Remap output is used where it has one). A Remap command has an <see cref="Output"/>; a Steps
/// command <see cref="RunsSteps"/>; a command with neither (no steps, its only step inactive, a key output with no key)
/// claims its input and does nothing, the way an override to nothing shadows a gesture.
/// </summary>
public sealed record HoldBinding(CommandId CommandId, string Name, HoldInput Input)
{
    /// <summary>The Remap step's output, played press for press; null for a Steps command or one that does nothing.</summary>
    public RemapOutput? Output { get; init; }

    /// <summary>True for a Steps command with at least one active step: its steps run once per press, once per wheel notch.</summary>
    public bool RunsSteps { get; init; }
}
