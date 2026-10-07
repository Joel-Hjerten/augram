using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.Imported;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Imported;

/// <summary>
/// The read-only form of an imported placeholder step (F8, C1): the StrokesPlus.net method, its
/// description, the raw parameters as "name: value" lines, and the sentence that says why it does
/// nothing yet. Never calls <c>changed</c>: there is nothing to edit until a real type replaces it.
/// </summary>
public sealed class ImportedStepForm : IStepForm
{
    public const string NotSupportedText = "Not supported yet: this step was imported from StrokesPlus.net and will run once Augram has a step type for it.";

    public string TypeKey => ImportedStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        var step = StepParameters.Expect<ImportedStep>(current, ImportedStepType.Instance);
        var parameters = step.Parameters.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, step.Parameters.Select(pair => pair.Key + ": " + pair.Value));
        var screen = new FormScreen("Imported step",
        [
            new Section("Imported step",
            [
                new NoteField("Method", string.IsNullOrEmpty(step.SourceMethod) ? "(unknown)" : step.SourceMethod),
                new NoteField("Description", string.IsNullOrEmpty(step.Description) ? "(none)" : step.Description),
                new NoteField("Parameters", parameters),
                new NoteField("Status", NotSupportedText),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
