using Augram.App.Components.Steps;
using Avalonia;

namespace Augram.App.Components.StepList;

/// <summary>
/// The command half of <see cref="StepList"/>'s forms (plan 0005): <see cref="FormContext"/> is what the expanded step's form
/// may adapt to about the selected command (its trigger here: the Remap form offers only a key output on a button trigger).
/// A new context rebuilds the expanded form, so it never shows choices for the command's previous trigger.
/// </summary>
public sealed partial class StepList
{
    public static readonly StyledProperty<StepFormContext> FormContextProperty =
        AvaloniaProperty.Register<StepList, StepFormContext>(nameof(FormContext), StepFormContext.None);

    /// <summary>The selected command as the step forms see it; <see cref="StepFormContext.None"/> without one.</summary>
    public StepFormContext FormContext
    {
        get => GetValue(FormContextProperty);
        set => SetValue(FormContextProperty, value);
    }

    private void RebuildForms()
    {
        foreach (var row in Rows)
        {
            row.Form = null;
            row.FormBuiltFor = null;
            row.LastEmitted = null;
        }

        ApplySelection();
    }
}
