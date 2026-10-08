using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.TypeText;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.TypeText;

/// <summary>
/// The Type text step's form (F5): the text in a multi-line field (Enter adds a line break, which the step presses as
/// the Enter key; long text wraps; the field grows to the theme's maximum height, then scrolls) and the method as a
/// dropdown. Every edit that changes the step emits one new <see cref="TypeTextStep"/>, typing included, one undo step each
/// like the app group form's text fields.
/// </summary>
public sealed class TypeTextStepForm : IStepForm
{
    public const string UnicodeLabel = "Unicode (works with most apps)";

    public const string KeysLabel = "Press keys (for games; US layout)";

    public const string TextHelp = "Typed into the window under the gesture start. Enter adds a line break, sent as the Enter key.";

    public const string MethodHelp = "Press keys types only what a US keyboard has (no å, é or €): for games and consoles that ignore Unicode input.";

    private static readonly IReadOnlyList<Choice<TypeTextMethod>> Methods =
    [
        new(UnicodeLabel, TypeTextMethod.Unicode),
        new(KeysLabel, TypeTextMethod.Keys),
    ];

    public string TypeKey => TypeTextStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<TypeTextStep>(current, TypeTextStepType.Instance);

        void Emit(TypeTextStep next)
        {
            if (next == state)
            {
                return;
            }

            state = next;
            changed(next);
        }

        var screen = new FormScreen("Type text",
        [
            new Section("Type text",
            [
                new TextField("Text", new DelegateBinding<string>(() => state.Text, text => Emit(state with { Text = text })), TextHelp)
                {
                    Multiline = true,
                },
                new DropdownField<TypeTextMethod>(
                    "Method",
                    Methods,
                    new DelegateBinding<TypeTextMethod>(() => state.Method, method => Emit(state with { Method = method })),
                    MethodHelp),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
