using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.WindowOp;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.WindowOp;

/// <summary>
/// The Window step's form (F5): the operation as a dropdown labelled by the step summaries, and width
/// and height fields that exist only while the operation is Set size (switching to it starts at
/// <see cref="DefaultWidth"/>×<see cref="DefaultHeight"/>). The sections are a declared
/// <see cref="FormScreen"/> (ADR-0002 §5c), re-declared when the operation changes shape.
/// </summary>
public sealed class WindowOpStepForm : IStepForm
{
    public const int DefaultWidth = 1280;

    public const int DefaultHeight = 720;

    private static readonly IReadOnlyList<Choice<WindowOperation>> Operations =
        [.. Enum.GetValues<WindowOperation>().Select(operation => new Choice<WindowOperation>(new WindowOpStep(operation).Summary, operation))];

    public string TypeKey => WindowOpStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var step = StepParameters.Expect<WindowOpStep>(current, WindowOpStepType.Instance);
        var host = new SectionForm.SectionForm();
        Show(host, step, changed);
        return host;
    }

    private static void Show(SectionForm.SectionForm host, WindowOpStep step, Action<IStep> changed)
    {
        var state = step;
        void Emit(WindowOpStep next)
        {
            var reshaped = (next.Operation == WindowOperation.SetSize) != (state.Operation == WindowOperation.SetSize);
            state = next;
            changed(next);
            if (reshaped)
            {
                Show(host, next, changed);
            }
        }

        var fields = new List<Field>
        {
            new DropdownField<WindowOperation>(
                "Operation",
                Operations,
                new DelegateBinding<WindowOperation>(() => state.Operation, operation => Emit(WithOperation(state, operation))),
                "Acts on the window under the gesture start, activated first (A20)."),
        };
        if (step.Operation == WindowOperation.SetSize)
        {
            fields.Add(new NumberField(
                "Width",
                new DelegateBinding<double>(() => state.Size?.Width ?? DefaultWidth, width => Emit(state with { Size = new WindowSize((int)width, state.Size?.Height ?? DefaultHeight) })),
                WindowOpStepType.MinSizePx,
                WindowOpStepType.MaxSizePx,
                Help: "Outer width in physical pixels."));
            fields.Add(new NumberField(
                "Height",
                new DelegateBinding<double>(() => state.Size?.Height ?? DefaultHeight, height => Emit(state with { Size = new WindowSize(state.Size?.Width ?? DefaultWidth, (int)height) })),
                WindowOpStepType.MinSizePx,
                WindowOpStepType.MaxSizePx,
                Help: "Outer height in physical pixels."));
        }

        host.Screen = new FormScreen("Window", [new Section("Window", fields)]);
    }

    private static WindowOpStep WithOperation(WindowOpStep step, WindowOperation operation) => operation == WindowOperation.SetSize
        ? new WindowOpStep(operation, step.Size ?? new WindowSize(DefaultWidth, DefaultHeight))
        : new WindowOpStep(operation);
}
