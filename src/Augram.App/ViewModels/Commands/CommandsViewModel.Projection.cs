using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.Core.Capture;
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
    private MouseButton? _strokeButton;
    private bool _projectingEdit;
    private int _optionsDragDistancePx = CaptureThresholds.Default.ButtonDragDistancePx;

    private void Select(SectionId? section, CommandId? command)
    {
        SelectedSectionId = section;
        SelectedCommandId = command;
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ProjectEdit();
        }
        else
        {
            Dispatcher.UIThread.Post(ProjectEdit);
        }
    }

    /// <summary>
    /// This machine's stroke button (Options › General), set by the composition root: a trigger naming it means the stroke
    /// button here, and its header says so. Null (no such note) in tests and the gallery.
    /// </summary>
    public MouseButton? StrokeButton
    {
        get => _strokeButton;
        set
        {
            if (_strokeButton != value)
            {
                _strokeButton = value;
                OnStoreChanged(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Options › Strokes › Capture's button drag distance, set by the composition root (plan 0004): what the header's Drag distance row
    /// calls the Options value ("Options value (10 px)"), the distance of a trigger without its own. The default elsewhere.
    /// </summary>
    public int OptionsDragDistancePx
    {
        get => _optionsDragDistancePx;
        set
        {
            if (_optionsDragDistancePx != value)
            {
                _optionsDragDistancePx = value;
                OnStoreChanged(this, EventArgs.Empty);
            }
        }
    }

    /// <summary><see cref="Project"/> after a store change, the one kind of change that nudges the platform filter (<see cref="PlatformFilterNudges"/>).</summary>
    private void ProjectEdit()
    {
        _projectingEdit = true;
        try
        {
            Project();
        }
        finally
        {
            _projectingEdit = false;
        }
    }

    private void Project()
    {
        Sections = CommandSections.For(Scope, _store.Current, _expanded, _gestures.Find, _platform, ShowOtherPlatforms, StrokeButton);
        CanUndo = _store.CanUndo;
        CanRedo = _store.CanRedo;
        ProjectSelection();
    }

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

    /// <summary>
    /// The selected row as the header shows it: with the trigger draft over it while one waits (<c>.TriggerDraft</c>) and the
    /// Options value its drag distance falls back to; without the Also in row where the button its stored trigger holds is this
    /// machine's stroke button (it holds nothing back here, as for the drag distance); with the step types "New step…" greys
    /// and why (<see cref="StepOffer.Refusals"/> over the draft's trigger while one waits), and for each step the types "Change
    /// to…" greys for it (<see cref="StepOffer.ChangeRefusals"/>, the same way).
    /// </summary>
    private CommandItem? HeaderItem(CommandItem? row)
    {
        if (WithDraft(row) is not { } item)
        {
            return null;
        }

        var command = _store.FindCommand(item.Id)?.Command;
        var drafted = command is null ? null : DraftedTrigger(item.Id);
        return item with
        {
            OptionsDragDistancePx = OptionsDragDistancePx,
            StrokeButton = StrokeButton,
            CanSetAlsoIn = item.CanSetAlsoIn && (StrokeButton is not { } stroke || row!.Trigger.Hold.ForStrokeButton(stroke).HandsBackDrags),
            StepRefusals = command is null ? item.StepRefusals : StepOffer.Refusals(StepTypes, command, _platform, drafted),
            ChangeRefusals = command is null
                ? item.ChangeRefusals
                : [.. command.StepsFor(_platform).Select((_, index) => StepOffer.ChangeRefusals(StepTypes, command, index, _platform, drafted))],
        };
    }

    /// <summary>Whether the command (else the section) is still there and only the platform filter keeps it out of the list.</summary>
    private bool HiddenByPlatformFilter(SectionId? section, CommandId? command)
    {
        if (ShowOtherPlatforms)
        {
            return false;
        }

        var all = CommandSections.For(Scope, _store.Current, _expanded, _gestures.Find, _platform, showOtherPlatforms: true, StrokeButton);
        return command is { } id ? all.SelectMany(item => item.Commands).Any(item => item.Id == id) : section is { } sectionId && all.Any(item => item.Id == sectionId);
    }

    /// <summary>The selected command's row in the current projection; null without one.</summary>
    private CommandItem? SelectedRow()
        => SelectedCommandId is { } id ? Sections.SelectMany(section => section.Commands).FirstOrDefault(command => command.Id == id) : null;

    /// <summary>
    /// Re-reads the selected command and its steps from the current projection: the section follows the
    /// command (a category change moves it), a selection that vanished is dropped, and the header shows the
    /// trigger draft while one waits (<c>.TriggerDraft</c>).
    /// </summary>
    private void ProjectSelection()
    {
        var (hadSection, hadCommand) = (SelectedSectionId, SelectedCommandId);
        var selected = SelectedRow();
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

        if (_projectingEdit && ((hadCommand is not null && SelectedCommandId is null) || (hadSection is not null && SelectedSectionId is null))
            && HiddenByPlatformFilter(hadSection, hadCommand))
        {
            PlatformFilterNudges++;
        }

        SelectedCommand = HeaderItem(selected);
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

        ProjectSidePanel();
    }
}
