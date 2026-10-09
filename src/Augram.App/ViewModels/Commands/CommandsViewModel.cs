using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.App.UsedBy;
using Augram.Core.Abstractions;
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
/// the rules live in <see cref="MappingRules"/> and only their messages show here. UI-only state
/// (selection, expanded sections) is all it owns, and the in-memory clipboard is shared by both tabs, so a
/// Global command copied on one pastes into an app group on the other; deleting it loses nothing.
/// Sections start collapsed (Joel, 2026-10-07: a long list otherwise); each tab's view model is a
/// process-lifetime singleton, so what the user opened stays open for the running session (tab
/// switches, closing and reopening the window) and starts collapsed again on the next launch. This file
/// holds the state and the dispatch; <c>.Projection</c> re-reads the store, <c>.GroupPanel</c> and <c>.CategoryPanel</c> keep
/// the selected app group's or category's form in step with it, and <c>.Commands</c>,
/// <c>.Sections</c>, <c>.Groups</c>, <c>.Categories</c> and <c>.Steps</c> hold the intents of each level.
/// </summary>
public sealed partial class CommandsViewModel : ObservableObject, IDisposable
{
    private readonly MappingStore _store;
    private readonly GestureLibrary _gestures;
    private readonly IGesturePickerPresenter _picker;
    private readonly IFormDialogPresenter _dialogs;
    private readonly IConfirmPresenter _confirm;
    private readonly CommandClipboard _clipboard;
    private readonly HostPlatform _platform;
    private readonly HashSet<SectionId> _expanded = [];
    private CommandId? _stepsOf;

    /// <summary><paramref name="clipboard"/> is shared by both tabs; <paramref name="platform"/> is what a new step is authored on (F8).</summary>
    public CommandsViewModel(
        CommandsScope scope,
        MappingStore store,
        GestureLibrary gestures,
        IGesturePickerPresenter picker,
        IFormDialogPresenter dialogs,
        IConfirmPresenter confirm,
        CommandClipboard clipboard,
        HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(clipboard);
        Scope = scope;
        _store = store;
        _gestures = gestures;
        _picker = picker;
        _dialogs = dialogs;
        _confirm = confirm;
        _clipboard = clipboard;
        _platform = platform;
        _store.Changed += OnStoreChanged;
        _gestures.Changed += OnStoreChanged;
        Project();
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

    /// <summary>The help line under the tree.</summary>
    public string Help => Scope == CommandsScope.Global
        ? "Global commands fire over every app unless the app's group overrides them. Sections are categories (select one to rename it or choose where it is used); Uncategorized holds the rest. Right-click a row for the menu; rename with the rename key. Deleting asks first; " + CommandsKeymap.Current.Undo + " brings it back."
        : "One section per app group; its commands win over Global in that app. Right-click a row for the menu; rename with the rename key. Deleting a group or a command asks first; " + CommandsKeymap.Current.Undo + " brings it back.";

    [ObservableProperty]
    public partial IReadOnlyList<SectionItem> Sections { get; private set; } = [];

    /// <summary>The selected section row, or the section of the selected command: where "New command" and a pasted command go.</summary>
    [ObservableProperty]
    public partial SectionId? SelectedSectionId { get; private set; }

    [ObservableProperty]
    public partial CommandId? SelectedCommandId { get; private set; }

    [ObservableProperty]
    public partial CommandItem? SelectedCommand { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<StepItem> Steps { get; private set; } = [];

    /// <summary>
    /// The side panel's form for the selected section header: an app group's on the Apps tab, a category's (name, Use on) on
    /// the Global tab; null while a command, Uncategorized or nothing is selected.
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

    /// <summary>Feedback for the last action: a rule message, or "Deleted X. Ctrl+Z undoes it."</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

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
        _expanded.Add(section);
        Select(section, id);
        Project();
        return true;
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        _gestures.Changed -= OnStoreChanged;
    }

    private void Dispatch(CommandTreeActionEventArgs e)
    {
        switch (e.Action)
        {
            case CommandTreeAction.Select:
                Select(e.Section?.Id, e.Command?.Id);
                ProjectSelection();
                break;
            case CommandTreeAction.ToggleExpanded when e.Section is { } section:
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
            case CommandTreeAction.Paste:
                PasteCommand(TargetOf(e.Section));
                break;
            case CommandTreeAction.SetTriggerKind when e.Command is { } command && e.Kind is { } kind:
                SetTriggerKind(command, kind);
                break;
            case CommandTreeAction.SetTriggerHold when e.Command is { } command && e.Hold is { } hold:
                SetTrigger(command.Id, current => current.WithHold(hold));
                break;
            case CommandTreeAction.SetWheelDirection when e.Command is { } command && e.Wheel is { } direction:
                SetTrigger(command.Id, current => Trigger.ForWheel(direction, current.Hold));
                break;
            case CommandTreeAction.SetUseOn when e.Command is { } command && e.UseOn is { } useOn:
                UpdateCommand(command.Id, stored => stored with { UseOn = useOn });
                break;
            case CommandTreeAction.UseConvertedOriginal when e.Command is { } command:
                UpdateCommand(command.Id, stored => stored.WithoutOwnVersion());
                Message = $"'{command.Name}' runs the converted original here again. {CommandsKeymap.Current.Undo} brings its own steps back.";
                break;
            case CommandTreeAction.MarkOwnVersionChecked when e.Command is { } command:
                UpdateCommand(command.Id, stored => stored.WithOwnVersionChecked(DateTimeOffset.UtcNow));
                break;
            case CommandTreeAction.SetCategory when e.Command is { } command && e.Category is { } category:
                SetCategory(command, category.Id);
                break;
            case CommandTreeAction.PickGesture when e.Command is { } command:
                _ = PickGestureAsync(command);
                break;
            case CommandTreeAction.Undo:
                _store.Undo();
                break;
            case CommandTreeAction.Redo:
                _store.Redo();
                break;
        }
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (MappingValidationException exception)
        {
            Message = exception.Message;
        }
        catch (KeyNotFoundException exception)
        {
            Message = exception.Message;
        }
    }
}
