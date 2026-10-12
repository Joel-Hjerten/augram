using Augram.App.Components.CommandHeader;
using Augram.App.Components.CommandTree;
using Augram.App.Components.SearchBox;
using Augram.Core.Capture;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The projection half of <see cref="CommandsViewModel"/>: every store or library change re-reads the
/// sections of this tab (<see cref="CommandSections"/>), undo/redo availability and the selection, on the
/// UI thread. Both tabs' view models listen to the one store, so an edit on one tab shows on the other. The search
/// (plan 0009) narrows what the tree shows (<see cref="CommandsViewModel.Sections"/>), while the selection, the header and the
/// side panel read from the sections before the search (<see cref="_allSections"/>), so a hidden selected row keeps its panel.
/// </summary>
public sealed partial class CommandsViewModel
{
    /// <summary>This tab's sections after the platform filter and before the search: what the selection is checked against.</summary>
    private IReadOnlyList<SectionItem> _allSections = [];

    private MouseButton? _strokeButton;
    private bool _projectingEdit;
    private int _optionsDragDistancePx = CaptureThresholds.Default.ButtonDragDistancePx;

    private void Select(SectionId? section, CommandId? command)
    {
        SelectedSectionId = section;
        SelectedCommandId = command;
    }

    protected override void OnStoreChangedOnUi() => ProjectEdit();

    /// <summary>
    /// This machine's stroke button (Options › General), followed from the settings store: a trigger naming it means the stroke
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
    /// Options › Strokes › Capture's button drag distance, followed from the settings store (plan 0004): what the header's Drag distance row
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

    private void FollowSettings((MouseButton StrokeButton, int DragDistancePx) settings)
    {
        StrokeButton = settings.StrokeButton;
        OptionsDragDistancePx = settings.DragDistancePx;
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
        _allSections = CommandSections.For(Scope, _store.Current, _expanded, _gestures.Find, _platform, ShowOtherPlatforms, StrokeButton);
        Sections = CommandSections.Narrow(_allSections, SearchQuery.Parse(SearchText));
        CanUndo = _store.CanUndo;
        CanRedo = _store.CanRedo;
        ProjectSelection();
    }

    /// <summary>
    /// The selected row as the header shows it: with the trigger draft over it while one waits (<see cref="TriggerEditor.WithDraft"/>) and the
    /// Options value its drag distance falls back to; without the Also in row where the button its stored trigger holds is this
    /// machine's stroke button (it holds nothing back here, as for the drag distance); with the step types "New step…" greys
    /// and why (<see cref="StepOffer.Refusals"/> over the draft's trigger while one waits), and for each step the types "Change
    /// to…" greys for it (<see cref="StepOffer.ChangeRefusals"/>, the same way).
    /// </summary>
    private CommandHeaderItem? HeaderItem(CommandItem? row)
    {
        if (_trigger.WithDraft(row is null ? null : new CommandHeaderItem(row)) is not { } item)
        {
            return null;
        }

        var command = _store.FindCommand(item.Id)?.Command;
        var drafted = command is null ? null : _trigger.DraftedTrigger(item.Id);
        return item with
        {
            OptionsDragDistancePx = OptionsDragDistancePx,
            StrokeButton = StrokeButton,
            StepSide = command is null ? null : StepSideOf(command),
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

    /// <summary>The selected command's row in the current projection (before the search); null without one.</summary>
    private CommandItem? SelectedRow()
        => SelectedCommandId is { } id ? _allSections.SelectMany(section => section.Commands).FirstOrDefault(command => command.Id == id) : null;

    /// <summary>
    /// Re-reads the selected command and its steps from the current projection: the section follows the
    /// command (a category change moves it), a selection that vanished is dropped, and the header shows the
    /// trigger draft while one waits (<see cref="TriggerEditor"/>).
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

        if (SelectedSectionId is { } sectionId && _allSections.All(section => section.Id != sectionId))
        {
            SelectedSectionId = null;
        }

        if (_projectingEdit && ((hadCommand is not null && SelectedCommandId is null) || (hadSection is not null && SelectedSectionId is null))
            && HiddenByPlatformFilter(hadSection, hadCommand))
        {
            PlatformFilterNudges++;
        }

        var otherCommand = _stepsOf != SelectedCommandId;
        if (otherCommand)
        {
            // Another command opens on this platform's steps (plan 0010 decision 1).
            _stepsView.Reset();
        }

        SelectedCommand = HeaderItem(selected);
        var steps = selected is null ? [] : StepItems(_store.FindCommand(selected.Id)!.Value.Command);
        Steps = steps;
        if (otherCommand)
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

    /// <summary>Shows the selected command in the header again, with the draft over its trigger while one waits.</summary>
    private void ShowSelected() => SelectedCommand = HeaderItem(SelectedRow());

    /// <summary>Shows the selected app group's, hold remap's or category's form (<see cref="SectionPanel"/>), or none.</summary>
    private void ProjectSidePanel()
        => _panel.Show(SelectedCommandId is null && SelectedSectionId is { } id ? _allSections.FirstOrDefault(s => s.Id == id) : null);
}
