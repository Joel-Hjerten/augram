using Augram.Core.Steps.Remap;

namespace Augram.Core.Mapping;

/// <summary>
/// The button trigger commands over one window whose one active step is a Remap step with a key set (plan 0005 decision 9),
/// and the drag trigger commands whose one active step is a Remap step with a button output (plan 0015 decision 3): the output
/// the engine worker holds itself while the trigger's buttons are down (a drag's from its start until the button's release),
/// never through the command executor, whose queue drops its oldest entry when full and could drop the release. Worked out off
/// the hook thread with the window's <see cref="Capture.AnchorPlan"/> (<see cref="AnchorPlanner.Answer"/>), from the same
/// commands in the same order: the app group's, then Global's not shadowed by one of them, none whose "Not in" claims the
/// window. Every other button or drag trigger command goes to the executor as a wheel trigger's does. Immutable; the hook
/// hands the reference over with each press.
/// </summary>
public sealed class ButtonOutputs
{
    private readonly ButtonOutput[] _outputs;

    private ButtonOutputs(ButtonOutput[] outputs) => _outputs = outputs;

    public static ButtonOutputs Empty { get; } = new([]);

    public IReadOnlyList<ButtonOutput> Outputs => _outputs;

    public bool IsEmpty => _outputs.Length == 0;

    /// <summary>The outputs in the order given (the resolver's: app commands first).</summary>
    public static ButtonOutputs Of(IEnumerable<ButtonOutput> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        var array = outputs.ToArray();
        return array.Length == 0 ? Empty : new ButtonOutputs(array);
    }

    /// <summary>The output whose trigger fires for <paramref name="pressed"/>, the first in order; null when none does (the executor resolves it then).</summary>
    public ButtonOutput? For(PressedTrigger pressed)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        foreach (var output in _outputs)
        {
            if (pressed.Matches(output.Trigger))
            {
                return output;
            }
        }

        return null;
    }
}

/// <summary>
/// One button or drag trigger command's held output: <see cref="Output"/> is a key with its modifiers for a button trigger
/// (plan 0005 decision 8), a button (with its modifiers and any held key) for a drag trigger (plan 0015 decisions 3 and 4).
/// </summary>
public sealed record ButtonOutput(CommandId CommandId, string Name, Trigger Trigger, RemapOutput Output);
