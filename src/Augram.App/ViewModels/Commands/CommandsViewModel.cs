using Augram.App.Components.CommandHeader;
using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.App.Components.ListEditing;
using Augram.App.Components.SearchBox;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.App.Dialogs;
using Augram.App.Hosting;
using Augram.App.Transfer;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// One Commands sub-tab's projection over <see cref="MappingStore"/> (F5a, F5, F3, F8; Global/Apps split,
/// Joel 2026-10-07): the sections of its <see cref="Scope"/> (<see cref="CommandSections"/>: the Global
/// group's categories, or the app groups), the selection, the selected command's steps, undo/redo
/// availability and the message line. Turns the tree's and the step list's intents into store calls;
/// the rules live in <see cref="MappingRules"/> and only their messages show here (a refused trigger edit
/// waits as a draft in the header, with its note, instead: <see cref="TriggerEditor"/>). UI-only state
/// (selection, expanded sections, the search text, the trigger draft) is all it owns, and the in-memory clipboard is shared by both tabs, so a
/// Global command copied on one pastes into an app group on the other; deleting it loses nothing.
/// Sections start collapsed (Joel, 2026-10-07: a long list otherwise); each tab's view model is a
/// process-lifetime singleton, so what the user opened stays open for the running session (tab
/// switches, closing and reopening the window) and starts collapsed again on the next launch. This file
/// holds the state and the dispatch; <c>.Projection</c> re-reads the store, and <c>.Commands</c>, <c>.Sections</c>,
/// <c>.Groups</c>, <c>.HoldRemaps</c>, <c>.Categories</c>, <c>.NotIn</c> and <c>.AlsoIn</c> hold the intents of each level. Three
/// internal collaborators hold the rest, each through <see cref="ICommandsHost"/>: <see cref="TriggerEditor"/> the header's
/// trigger and input edits with their draft, Swap and Take it; <see cref="StepEditor"/> the step list's intents
/// (<c>.Steps</c> is their entry); <see cref="SectionPanel"/> the side panel's forms.
/// </summary>
public sealed partial class CommandsViewModel : StoreViewModel, IDisposable, ICommandsHost
{
    private readonly MappingStore _store;
    private readonly GestureLibrary _gestures;
    private readonly IFormDialogPresenter _dialogs;
    private readonly CommandClipboard _clipboard;
    private readonly HostPlatform _platform;
    private readonly HashSet<SectionId> _expanded = [];
    private readonly SettingsFollower<(MouseButton StrokeButton, int DragDistancePx)>? _settings;
    private readonly TriggerEditor _trigger;
    private readonly StepEditor _steps;
    private readonly SectionPanel _panel;
    private CommandId? _stepsOf;
    private (SectionId? Section, CommandId? Command)? _selectionBeforeSearch;

    /// <summary>
    /// <paramref name="clipboard"/> is shared by both tabs; <paramref name="platform"/> is what a new step is authored on (F8);
    /// <paramref name="export"/> runs a section menu's Export… (plan 0003), null where there is none (tests, the gallery);
    /// <paramref name="settings"/> is followed for <see cref="StrokeButton"/> and <see cref="OptionsDragDistancePx"/>, null in tests and the gallery.
    /// </summary>
    public CommandsViewModel(
        CommandsScope scope,
        MappingStore store,
        GestureLibrary gestures,
        IGesturePickerPresenter picker,
        IFormDialogPresenter dialogs,
        IConfirmPresenter confirm,
        CommandClipboard clipboard,
        HostPlatform platform,
        IExportPresenter? export = null,
        SettingsStore? settings = null)
        : base(confirm, MappingRefusals, export)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(clipboard);
        Scope = scope;
        _store = store;
        _gestures = gestures;
        _dialogs = dialogs;
        _clipboard = clipboard;
        _platform = platform;
        _trigger = new TriggerEditor(store, gestures, picker, platform, this);
        _steps = new StepEditor(store, clipboard, platform, _trigger, this);
        _panel = new SectionPanel(store, this);
        _store.Changed += OnStoreChanged;
        _gestures.Changed += OnStoreChanged;
        Project();
        if (settings is not null)
        {
            // This machine's stroke button: a trigger naming it means the stroke button here, which the header says. And the
            // Options value of the button drag distance, which a trigger without its own uses (the header's Drag distance row).
            _settings = new SettingsFollower<(MouseButton StrokeButton, int DragDistancePx)>(
                settings,
                current => (current.General.StrokeButton, current.Capture.ButtonDragDistancePx),
                (_, next) => FollowSettings(next));
            FollowSettings(_settings.Current);
        }
    }

    /// <summary>The tree should start renaming this command in place (a fresh "New command N").</summary>
    public event EventHandler<CommandId>? RenameRequested;

    /// <summary>The tree should start renaming this section in place (a fresh "New category N").</summary>
    public event EventHandler<SectionId>? SectionRenameRequested;

    public CommandsScope Scope { get; }

    /// <summary>The tree's title.</summary>
    public string Heading => Scope == CommandsScope.Global ? "Global commands" : "App groups";

    /// <summary>The new-section button: a category on the Global tab, an app group on the Apps tab.</summary>
    public string NewSectionLabel => Scope == CommandsScope.Global ? "New category" : "New group";

    /// <summary>The platform filter toggle (F8 "Use on"): app groups and commands not used here are hidden until it is on.</summary>
    public string? PlatformFilterLabel => "Show other platforms";

    /// <summary>List the groups used only on the other platform too, greyed. Session state, like the expanded sections; off at start.</summary>
    [ObservableProperty]
    public partial bool ShowOtherPlatforms { get; private set; }

    /// <summary>
    /// The search box's text (plan 0009): UI-only, two-way bound from the tree's box, applied by <see cref="CommandSections.Narrow"/>
    /// after the platform filter (decision 4) on every change; never persisted, cleared when the main window hides
    /// (<c>Hosting/HiddenWindowReset</c>, decision 8). The selection survives a search that hides it (decision 5): the header
    /// and side panel stay, and clearing the box shows the row again, its section opened in the saved expanded set.
    /// </summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Counts the edits that hid the selected row behind the platform filter (its Use on, or its category's or group's, left this
    /// platform out while <see cref="ShowOtherPlatforms"/> is off); the tree blinks the toggle at each, so the user sees where it
    /// went (Joel, 0.11.9). A row that was deleted, or one the user hid by turning the filter off, does not count.
    /// </summary>
    [ObservableProperty]
    public partial int PlatformFilterNudges { get; private set; }

    /// <summary>The help line under the tree.</summary>
    public string Help => Scope == CommandsScope.Global
        ? "Global commands fire over every app unless the app's group overrides them. Sections are categories (select one to rename it, choose where it is used, or add a command with New command on its row); a command left without one goes into Uncategorized. Right-click a row for the menu; rename with the rename key. Deleting asks first; " + EditKeymap.Current.Undo + " brings it back."
        : "One section per app group; its commands win over Global in that app. Select a group, and New command on its row adds a command to it. A group's hold remaps sit inside it with their commands (right-click a group: New hold remap). Right-click a row for the menu; rename with the rename key. Deleting a group, a hold remap or a command asks first; " + EditKeymap.Current.Undo + " brings it back.";

    [ObservableProperty]
    public partial IReadOnlyList<SectionItem> Sections { get; private set; } = [];

    /// <summary>The selected section row, or the section of the selected command: where "New command" and a pasted command go.</summary>
    [ObservableProperty]
    public partial SectionId? SelectedSectionId { get; private set; }

    [ObservableProperty]
    public partial CommandId? SelectedCommandId { get; private set; }

    [ObservableProperty]
    public partial CommandHeaderItem? SelectedCommand { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<StepItem> Steps { get; private set; } = [];

    /// <summary>
    /// The side panel's form for the selected section header: an app group's on the Apps tab, a category's (name, Use on) on
    /// the Global tab; null while a command or nothing is selected.
    /// </summary>
    [ObservableProperty]
    public partial FormScreen? GroupForm { get; private set; }

    /// <summary>The expanded step of the selected command; -1 for none.</summary>
    [ObservableProperty]
    public partial int SelectedStepIndex { get; private set; } = -1;

    [ObservableProperty]
    public partial bool CanUndo { get; private set; }

    [ObservableProperty]
    public partial bool CanRedo { get; private set; }

    /// <summary>What "New step…" offers; the picker drops the <c>Other</c> category itself.</summary>
    public IReadOnlyList<IStepType> StepTypes { get; } = StepRegistry.BuiltIn.All;

    public void Handle(CommandTreeActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        Guard(() => Dispatch(e));
    }

    /// <summary>Expands the command's section and selects it (the "Used by…" jump); false when it no longer exists or belongs to the other tab.</summary>
    public bool ShowCommand(CommandId id)
    {
        if (_store.FindCommand(id) is not { } found || !CommandSections.Includes(Scope, found.Group))
        {
            return false;
        }

        var section = CommandSections.SectionOf(Scope, found.Group, found.Command);
        Expand(section);
        Select(section, id);
        Project();
        return true;
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        _gestures.Changed -= OnStoreChanged;
        _settings?.Dispose();
    }

    partial void OnSearchTextChanged(string value)
    {
        if (!SearchQuery.Parse(value).IsEmpty)
        {
            _selectionBeforeSearch ??= (SelectedSectionId, SelectedCommandId);
        }
        else if (_selectionBeforeSearch is { } before)
        {
            // Back from a search: the tree is as it was before it (Joel, 2026-10-12). Only a row selected during the search
            // gets its section opened, so the row is in sight (decision 5); a selection made before the search was in a
            // collapsed section on purpose and stays so.
            _selectionBeforeSearch = null;
            if (SelectedSectionId is { } section && before != (SelectedSectionId, SelectedCommandId))
            {
                Expand(section);
            }
        }

        Project();
    }

    private void Dispatch(CommandTreeActionEventArgs e)
    {
        switch (e.Action)
        {
            case CommandTreeAction.Select:
                Select(e.Section?.Id, e.Command?.Id);
                ProjectSelection();
                break;
            case CommandTreeAction.ToggleExpanded when e.Section is { } section && SearchQuery.Parse(SearchText).IsEmpty:
                // While searching every shown section is expanded by the rule and the saved set is left alone (decision 4).
                ToggleExpanded(section);
                break;
            case CommandTreeAction.NewSection:
                NewSection();
                break;
            case CommandTreeAction.ToggleOtherPlatforms:
                ShowOtherPlatforms = !ShowOtherPlatforms;
                Project();
                break;
            case CommandTreeAction.NewCommand:
                NewCommand(TargetOf(e.Section));
                break;
            case CommandTreeAction.NewHoldRemap when e.Section is { } section:
                NewHoldRemap(section);
                break;
            case CommandTreeAction.Rename when e.Command is { } command && e.Name is { } name:
                UpdateCommand(command.Id, stored => stored with { Name = name });
                break;
            case CommandTreeAction.Rename when e.Section is { } section && e.Name is { } name:
                RenameSection(section, name);
                break;
            case CommandTreeAction.Delete when e.Command is { } command:
                _ = DeleteCommandAsync(command);
                break;
            case CommandTreeAction.Delete when e.Section is { } section:
                _ = DeleteSectionAsync(section);
                break;
            case CommandTreeAction.ToggleActive when e.Command is { } command:
                UpdateCommand(command.Id, stored => stored with { IsActive = !stored.IsActive });
                break;
            case CommandTreeAction.ToggleActive when e.Section is { } section:
                ToggleSectionActive(section);
                break;
            case CommandTreeAction.Copy when e.Command is { } command:
                CopyCommand(command);
                break;
            case CommandTreeAction.Copy when e.Section is { Id.HoldRemapId: { } holdRemap } section:
                CopyHoldRemap(section.Id.GroupId, holdRemap);
                break;
            case CommandTreeAction.Paste:
                PasteCommand(PasteTargetOf(e.Section));
                break;
            case CommandTreeAction.Export when e.Section is { CanExport: true } section:
                _ = ExportAsync(section);
                break;
            case CommandTreeAction.SetTriggerKind or CommandTreeAction.SetTriggerHold or CommandTreeAction.SetWheelDirection or CommandTreeAction.SetTriggerButton
                or CommandTreeAction.SetInputKind or CommandTreeAction.SetInput or CommandTreeAction.SwapTrigger or CommandTreeAction.TakeTrigger:
                _trigger.Dispatch(e);
                break;
            case CommandTreeAction.SetUseOn when e.Command is { } command && e.UseOn is { } useOn:
                UpdateCommand(command.Id, stored => stored with { UseOn = useOn });
                break;
            case CommandTreeAction.EditNotIn when e.Command is { } command:
                _ = EditNotInAsync(command);
                break;
            case CommandTreeAction.EditAlsoIn when e.Command is { } command:
                _ = EditAlsoInAsync(command);
                break;
            case CommandTreeAction.UseConvertedOriginal when e.Command is { } command:
                UpdateCommand(command.Id, stored => stored.WithoutOwnVersion());
                Message = $"'{command.Name}' runs the converted original here again. {EditKeymap.Current.Undo} brings its own steps back.";
                break;
            case CommandTreeAction.MarkOwnVersionChecked when e.Command is { } command:
                UpdateCommand(command.Id, stored => stored.WithOwnVersionChecked(DateTimeOffset.UtcNow));
                break;
            case CommandTreeAction.SetCategory when e.Command is { } command && e.Category is { } category:
                SetCategory(command, category.Id);
                break;
            case CommandTreeAction.PickGesture when e.Command is { } command:
                _ = _trigger.PickGestureAsync(command);
                break;
            case CommandTreeAction.Undo:
                _store.Undo();
                break;
            case CommandTreeAction.Redo:
                _store.Redo();
                break;
        }
    }

    string? ICommandsHost.Message
    {
        get => Message;
        set => Message = value;
    }

    int ICommandsHost.SelectedStepIndex
    {
        get => SelectedStepIndex;
        set => SelectedStepIndex = value;
    }

    bool ICommandsHost.Guard(Action action) => Guard(action);

    void ICommandsHost.ShowSelected() => ShowSelected();

    void ICommandsHost.Reveal(CommandId id, bool otherPlatforms)
    {
        ShowOtherPlatforms |= otherPlatforms;
        ShowCommand(id);
    }

    void ICommandsHost.ShowForm(FormScreen? form) => GroupForm = form;
}
