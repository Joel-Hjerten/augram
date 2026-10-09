using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Remap;

/// <summary>
/// Placeholder form of the Remap step (Core <c>Steps/Remap</c>, plan 0002 step 1): shows the output read-only until plan
/// 0002 step 4 builds the real form (button / key / wheel output, modifier toggles). Never calls <c>changed</c>.
/// </summary>
public sealed class RemapStepForm : IStepForm
{
    public const string PendingText = "A Remap step plays its output while the hold key is held. Editing it here comes with the hold remap screens.";

    public string TypeKey => RemapStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        var step = StepParameters.Expect<RemapStep>(current, RemapStepType.Instance);
        var screen = new FormScreen("Remap",
        [
            new Section("Remap",
            [
                new NoteField("Output", step.Summary),
                new NoteField("Status", PendingText),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
