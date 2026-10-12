using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Delay;

/// <summary>The Delay step's form (F5): one number field, milliseconds within the type's range; an edit that changes nothing emits nothing.</summary>
public sealed class DelayStepForm : IStepForm
{
    public string TypeKey => DelayStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var form = new StepFormState<DelayStep>(StepParameters.Expect<DelayStep>(current, DelayStepType.Instance), changed);
        var screen = new FormScreen("Delay",
        [
            new Section("Delay",
            [
                new NumberField(
                    "Milliseconds",
                    form.Bind<double>(() => form.Current.Milliseconds, milliseconds => form.Emit(new DelayStep((int)milliseconds))),
                    DelayStepType.MinMilliseconds,
                    DelayStepType.MaxMilliseconds,
                    10,
                    "Waits before the next step. Not the settle delay: that one the executor adds itself after moving focus."),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
