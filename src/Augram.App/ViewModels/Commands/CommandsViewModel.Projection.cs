using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.Core.Mapping;
using Avalonia.Threading;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The projection half of <see cref="CommandsViewModel"/>: every store or library change re-reads the
/// sections of this tab (<see cref="CommandSections"/>), undo/redo availability and the selection, on the
/// UI thread. Both tabs' view models listen to the one store, so an edit on one tab shows on the other.
/// </summary>
public sealed partial class CommandsViewModel
{
    private void Select(SectionId? section, CommandId? command)
    {
        SelectedSectionId = section;
        SelectedCommandId = command;
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Project();
        }
        else
        {
            Dispatcher.UIThread.Post(Project);
        }
    }

    private void Project()
    {
        Sections = CommandSections.For(Scope, _store.Current, _expanded, _gestures.Find, _platform, ShowOtherPlatforms);
        CanUndo = _store.CanUndo;
        CanRedo = _store.CanRedo;
        ProjectSelection();
    }

    /// <summary>
    /// Re-reads the selected command and its steps from the current projection: the section follows the
    /// command (a category change moves it), and a selection that vanished is dropped.
    /// </summary>
    /// <summary>
    /// This platform's steps (F8): each row reads as the stored step does here ("Ctrl+W → Cmd+W"), and the step its form edits
    /// is the one an edit here starts from, the converted step where this platform has no own steps yet.
    /// </summary>
    private List<StepItem> StepItems(Command command)
    {
        var shown = command.StepsFor(_platform);
        var editable = EditableSteps(command);
        return [.. shown.Select((step, index) => StepItem.From(step, index, _platform) with { Step = editable[index] })];
    }

    private void ProjectSelection()
    {
        var selected = SelectedCommandId is { } id ? Sections.SelectMany(section => section.Commands).FirstOrDefault(command => command.Id == id) : null;
        if (selected is null)
        {
            SelectedCommandId = null;
        }
        else
        {
            SelectedSectionId = selected.Section;
        }

        if (SelectedSectionId is { } sectionId && Sections.All(section => section.Id != sectionId))
        {
            SelectedSectionId = null;
        }

        SelectedCommand = selected;
        var steps = selected is null ? [] : StepItems(_store.FindCommand(selected.Id)!.Value.Command);
        Steps = steps;
        if (_stepsOf != SelectedCommandId)
        {
            _stepsOf = SelectedCommandId;
            SelectedStepIndex = -1;
        }
        else if (SelectedStepIndex >= steps.Count)
        {
            SelectedStepIndex = steps.Count - 1;
        }

        ProjectGroupPanel();
    }
}
