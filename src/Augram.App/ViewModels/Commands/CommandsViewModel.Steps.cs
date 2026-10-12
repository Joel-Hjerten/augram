using Augram.App.Components.StepList;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The step list's entry of <see cref="CommandsViewModel"/> (F5a step editing): each intent runs inside <c>Guard</c> through
/// <see cref="StepEditor"/>, which turns it into one <c>UpdateCommand</c> on the selected command.
/// </summary>
public sealed partial class CommandsViewModel
{
    public void Handle(StepListActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        if (!Guard(() => _steps.Dispatch(e)) && e.Action == StepListAction.Edit && SelectedCommandId is not null)
        {
            // A refused edit (a rule on the Remap output, plan 0002) changed nothing stored: hand the list the stored steps
            // again, so the form that emitted it is rebuilt on them while the message line says why.
            Steps = [.. Steps];
        }
    }
}
