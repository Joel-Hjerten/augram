using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.Unknown;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Unknown;

/// <summary>
/// The read-only form of a step this Augram cannot read (Core <c>Steps/Unknown</c>): its type, why it could not be read,
/// and the sentence that says it is kept and runs once Augram is updated. Never calls <c>changed</c>.
/// </summary>
public sealed class UnknownStepForm : IStepForm
{
    public const string KeptText = "Kept as is: a newer Augram made this step. This version cannot show or run it, and keeps it unchanged when it saves. Update Augram on this machine to use it.";

    public string TypeKey => UnknownStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        var step = StepParameters.Expect<UnknownStep>(current, UnknownStepType.Instance);
        var screen = new FormScreen("Step from a newer Augram",
        [
            new Section("Step from a newer Augram",
            [
                new NoteField("Type", step.StoredTypeKey),
                new NoteField("Why", step.Reason),
                new NoteField("Status", KeptText),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
