using Augram.App.Components.Steps.DisplayMode;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hdr;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Hdr;

/// <summary>The HDR step's form: Toggle / On / Off, labelled by the step summaries, and the target display (the Display mode form's list).</summary>
public sealed class HdrStepForm : IStepForm
{
    private static readonly IReadOnlyList<Choice<HdrAction>> Actions =
        [.. Enum.GetValues<HdrAction>().Select(action => new Choice<HdrAction>(new HdrStep(action).Summary, action))];

    public string TypeKey => HdrStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<HdrStep>(current, HdrStepType.Instance);
        void Emit(HdrStep next)
        {
            state = next;
            changed(next);
        }

        var screen = new FormScreen("HDR",
        [
            new Section("HDR",
            [
                new DropdownField<HdrAction>(
                    "Action",
                    Actions,
                    new DelegateBinding<HdrAction>(() => state.Action, action => Emit(state with { Action = action })),
                    "Windows' \"Use HDR\" switch; Win+Alt+B does the same from the keyboard. macOS lets no app switch HDR, so there the step is skipped."),
                new DropdownField<DisplayTarget>(
                    "Display",
                    DisplayModeChoices.Targets,
                    new DelegateBinding<DisplayTarget>(() => state.Target, target => Emit(state with { Target = target })),
                    "With two displays: the one the gesture was drawn on, or always the main one."),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
