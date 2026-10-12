using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.MediaKey;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.MediaKey;

/// <summary>The Media key step's form (F5): one dropdown over <see cref="MediaKeyKind"/>, labelled by the step summaries; an edit that changes nothing emits nothing.</summary>
public sealed class MediaKeyStepForm : IStepForm
{
    private static readonly IReadOnlyList<Choice<MediaKeyKind>> Keys =
        [.. Enum.GetValues<MediaKeyKind>().Select(kind => new Choice<MediaKeyKind>(new MediaKeyStep(kind).Summary, kind))];

    public string TypeKey => MediaKeyStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var form = new StepFormState<MediaKeyStep>(StepParameters.Expect<MediaKeyStep>(current, MediaKeyStepType.Instance), changed);
        var screen = new FormScreen("Media key",
        [
            new Section("Media key",
            [
                new DropdownField<MediaKeyKind>(
                    "Key",
                    Keys,
                    form.Bind(() => form.Current.Key, key => form.Emit(new MediaKeyStep(key))),
                    "Tapped once; the same key on every platform."),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
