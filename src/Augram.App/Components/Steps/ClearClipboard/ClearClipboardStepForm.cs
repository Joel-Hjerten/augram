using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.ClearClipboard;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.ClearClipboard;

/// <summary>The Clear clipboard step's form: nothing to set, one line saying what it does. Never calls <c>changed</c>.</summary>
public sealed class ClearClipboardStepForm : IStepForm
{
    public const string WhatText = "Empties the clipboard, so nothing is left to paste. The same on every platform; nothing to set.";

    public string TypeKey => ClearClipboardStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        StepParameters.Expect<ClearClipboardStep>(current, ClearClipboardStepType.Instance);
        var screen = new FormScreen(ClearClipboardStep.Text,
        [
            new Section(ClearClipboardStep.Text,
            [
                new NoteField("Does", WhatText),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
