using Augram.App.Components.CommandHeader;
using Augram.App.Components.CommandTree;
using Augram.App.Components.ListEditing;
using Augram.App.Components.StepList;
using Augram.App.ViewModels.Commands.Projection;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The step list's entry of <see cref="CommandsViewModel"/> (F5a step editing): each intent runs inside <c>Guard</c> through
/// <see cref="StepEditor"/>, which turns it into one <c>UpdateCommand</c> on the selected command. And the "Steps for" switch
/// (plan 0010): which platform's steps are in view (<see cref="_stepsView"/>, UI state beside the selection, back on this
/// platform whenever another command is selected), the other platform's shown read-only, and Copy from the other platform.
/// </summary>
public sealed partial class CommandsViewModel
{
    public void Handle(StepListActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        if (!_stepsView.IsHere && e.Action is not (StepListAction.Undo or StepListAction.Redo))
        {
            // The other platform's steps are read, never edited here (decision 6): only Copy from replaces them.
            Message = $"These are the {_stepsView.Shown.DisplayName()} steps. {PlatformSideView.EditNote(_stepsView.Shown)}.";
            return;
        }

        if (!Guard(() => _steps.Dispatch(e)) && e.Action == StepListAction.Edit && SelectedCommandId is not null)
        {
            // A refused edit (a rule on the Remap output, plan 0002) changed nothing stored: hand the list the stored steps
            // again, so the form that emitted it is rebuilt on them while the message line says why.
            Steps = [.. Steps];
        }
    }

    /// <summary>The switch: shows <paramref name="platform"/>'s steps, none of them open. A view change, never an edit.</summary>
    private void ShowStepsFor(HostPlatform platform)
    {
        _stepsView.Shown = platform;
        SelectedStepIndex = -1;
        ProjectSelection();
    }

    /// <summary>
    /// Copy from the other platform (decision 5) into the side in view: its own steps go and it takes the other side's, so it
    /// is inferred again. On the own version's side that drops the own version; on the original's side the own version's
    /// steps become the original. One store call, so one undo step; offered only while the command has an own version.
    /// </summary>
    private void CopyStepsFromOtherPlatform(CommandItem item)
    {
        var (group, command) = _store.RequireCommand(item.Id);
        if (command.OwnVersion is not { } own)
        {
            return;
        }

        var side = _stepsView.Shown;
        _store.UpdateCommand(group.Id, own.Platform == side ? command.WithoutOwnVersion() : command.WithOwnVersionAsOriginal());
        SelectedStepIndex = -1;
        Message = $"The {side.DisplayName()} steps of '{command.Name}' are the {side.Other().DisplayName()} steps now, converted. {EditKeymap.Current.Undo} brings them back.";
    }

    /// <summary>
    /// The steps in view as rows. This platform's (F8): each row reads as the stored step does here ("Ctrl+W → Cmd+W"), and the
    /// step its form edits is the one an edit here starts from, the converted step where this platform has no own steps yet.
    /// The other platform's: what runs there, each step read in its key names, converted ones with their markers.
    /// </summary>
    private List<StepItem> StepItems(Command command)
    {
        var shown = _stepsView.Shown;
        if (!_stepsView.IsHere)
        {
            return [.. command.StepsFor(shown).Select((step, index) => StepItemProjection.From(step, index, shown))];
        }

        var editable = StepEditor.EditableSteps(command, shown);
        return [.. command.StepsFor(shown).Select((step, index) => StepItemProjection.From(step, index, shown) with { Step = editable[index] })];
    }

    /// <summary>
    /// The header's Steps for line (plan 0010; Joel, 0.14.2: no label, the outline marks this computer's platform): the side in
    /// view, on the other side its note "Edit on Windows", Copy from while both sides have their own steps, and Mark as checked for
    /// this platform's own steps made before the original last changed. Where steps come from shows on the rows and the command row.
    /// </summary>
    private StepSide StepSideOf(Command command)
    {
        var shown = _stepsView.Shown;
        var ownVersionHere = command.OwnVersion?.Platform == shown;
        return new StepSide(
            shown,
            _stepsView.IsHere,
            _stepsView.IsHere ? null : PlatformSideView.EditNote(shown),
            command.OwnVersion is null ? null : $"Copy from {shown.Other().DisplayName()}",
            _stepsView.IsHere && ownVersionHere && command.IsOwnVersionStale);
    }
}
