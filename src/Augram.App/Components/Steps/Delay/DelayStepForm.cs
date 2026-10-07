using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Delay;

/// <summary>The Delay step's form (F5): one number field, milliseconds within the type's range.</summary>
public sealed class DelayStepForm : IStepForm
{
    public string TypeKey => DelayStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<DelayStep>(current, DelayStepType.Instance);
        var screen = new FormScreen("Delay",
        [
            new Section("Delay",
            [
                new NumberField(
                    "Milliseconds",
                    new DelegateBinding<double>(() => state.Milliseconds, milliseconds =>
                    {
                        state = new DelayStep((int)milliseconds);
                        changed(state);
                    }),
                    DelayStepType.MinMilliseconds,
                    DelayStepType.MaxMilliseconds,
                    10,
                    "Waits before the next step. Not the settle delay: that one the executor adds itself after moving focus."),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
