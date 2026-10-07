using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.MediaKey;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.MediaKey;

/// <summary>The Media key step's form (F5): one dropdown over <see cref="MediaKeyKind"/>, labelled by the step summaries.</summary>
public sealed class MediaKeyStepForm : IStepForm
{
    private static readonly IReadOnlyList<Choice<MediaKeyKind>> Keys =
        [.. Enum.GetValues<MediaKeyKind>().Select(kind => new Choice<MediaKeyKind>(new MediaKeyStep(kind).Summary, kind))];

    public string TypeKey => MediaKeyStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<MediaKeyStep>(current, MediaKeyStepType.Instance);
        var screen = new FormScreen("Media key",
        [
            new Section("Media key",
            [
                new DropdownField<MediaKeyKind>(
                    "Key",
                    Keys,
                    new DelegateBinding<MediaKeyKind>(() => state.Key, key =>
                    {
                        state = new MediaKeyStep(key);
                        changed(state);
                    }),
                    "Tapped once; the same key on every platform."),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
