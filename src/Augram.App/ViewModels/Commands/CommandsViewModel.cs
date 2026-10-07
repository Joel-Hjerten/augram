using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.Components.StepList;
using Augram.App.UsedBy;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The Commands tab's projection over <see cref="MappingStore"/> (F5a, F5, F3, F8): the groups with
/// their commands (Global first, then by name, as the store sorts them), the selection, the selected
/// command's steps, undo/redo availability and the message line. Turns the tree's and the step list's
/// intents into store calls; the rules live in <see cref="MappingRules"/> and only their messages
/// show here. UI-only state (selection, collapsed groups, the clipboard) is all it owns; deleting it
/// loses nothing. This file holds the state and the dispatch; the partials <c>.Commands</c>,
/// <c>.Groups</c> and <c>.Steps</c> hold the intents of each level.
/// </summary>
public sealed partial class CommandsViewModel : ObservableObject, IDisposable
{
    private readonly MappingStore _store;
    private readonly GestureLibrary _gestures;
    private readonly IGesturePickerPresenter _picker;
    private readonly IFormDialogPresenter _dialogs;
    private readonly IConfirmPresenter _confirm;
    private readonly HostPlatform _platform;
    private readonly CommandClipboard _clipboard = new();
    private readonly HashSet<GroupId> _collapsed = [];
    private CommandId? _stepsOf;

    /// <summary><paramref name="platform"/> is what a new step is authored on (F8).</summary>
    public CommandsViewModel(
        MappingStore store,
        GestureLibrary gestures,
        IGesturePickerPresenter picker,
        IFormDialogPresenter dialogs,
        IConfirmPresenter confirm,
        HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(confirm);
        _store = store;
        _gestures = gestures;
        _picker = picker;
        _dialogs = dialogs;
        _confirm = confirm;
        _platform = platform;
        _store.Changed += OnStoreChanged;
        _gestures.Changed += OnStoreChanged;
        Project();
    }

    /// <summary>The tree should start renaming this command in place (a fresh "New command N").</summary>
    public event EventHandler<CommandId>? RenameRequested;

    [ObservableProperty]
    public partial IReadOnlyList<GroupItem> Groups { get; private set; } = [];

    /// <summary>The selected group row, or the group of the selected command: where "New command" and a pasted command go.</summary>
    [ObservableProperty]
    public partial GroupId? SelectedGroupId { get; private set; }

    [ObservableProperty]
    public partial CommandId? SelectedCommandId { get; private set; }

    [ObservableProperty]
    public partial CommandItem? SelectedCommand { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<StepItem> Steps { get; private set; } = [];

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

    /// <summary>Expands the command's group and selects it (the "Used by…" jump); false when it no longer exists.</summary>
    public bool ShowCommand(CommandId id)
    {
        if (_store.FindCommand(id) is not { } found)
        {
            return false;
        }

        _collapsed.Remove(found.Group.Id);
        Select(found.Group.Id, id);
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
                Select(e.Group?.Id, e.Command?.Id);
                ProjectSelection();
                break;
            case CommandTreeAction.ToggleExpanded when e.Group is { } group:
                if (!_collapsed.Remove(group.Id))
                {
                    _collapsed.Add(group.Id);
                }

                Project();
                break;
            case CommandTreeAction.NewGroup:
                _ = NewGroupAsync();
                break;
            case CommandTreeAction.EditGroup when e.Group is { IsGlobal: false } group:
                _ = EditGroupAsync(group.Id);
                break;
            case CommandTreeAction.NewCommand:
                NewCommand(e.Group?.Id ?? SelectedGroupId ?? GroupId.Global);
                break;
            case CommandTreeAction.Rename when e.Command is { } command && e.Name is { } name:
                UpdateCommand(command.Id, stored => stored with { Name = name });
                break;
            case CommandTreeAction.Rename when e.Group is { } group && e.Name is { } name:
                RenameGroup(group, name);
                break;
            case CommandTreeAction.Delete when e.Command is { } command:
                _ = DeleteCommandAsync(command);
                break;
            case CommandTreeAction.Delete when e.Group is { } group:
                _ = DeleteGroupAsync(group);
                break;
            case CommandTreeAction.ToggleActive when e.Command is { } command:
                UpdateCommand(command.Id, stored => stored with { IsActive = !stored.IsActive });
                break;
            case CommandTreeAction.ToggleActive when e.Group is { } group:
                _store.UpdateGroup(RequireGroup(group.Id) with { IsActive = !group.IsActive });
                break;
            case CommandTreeAction.Copy when e.Command is { } command:
                CopyCommand(command);
                break;
            case CommandTreeAction.Paste:
                PasteCommand(e.Group?.Id ?? SelectedGroupId ?? GroupId.Global);
                break;
            case CommandTreeAction.SetTriggerKind when e.Command is { } command && e.Kind is { } kind:
                SetTriggerKind(command, kind);
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

    private void Select(GroupId? group, CommandId? command)
    {
        SelectedGroupId = group;
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
        Groups = _store.Current.Groups.Select(group => GroupItem.From(group, !_collapsed.Contains(group.Id), _gestures.Find)).ToList();
        CanUndo = _store.CanUndo;
        CanRedo = _store.CanRedo;
        ProjectSelection();
    }

    /// <summary>Re-reads the selected command and its steps from the current projection; drops a selection that vanished.</summary>
    private void ProjectSelection()
    {
        if (SelectedGroupId is { } groupId && Groups.All(group => group.Id != groupId))
        {
            SelectedGroupId = null;
        }

        var selected = SelectedCommandId is { } id ? Groups.SelectMany(group => group.Commands).FirstOrDefault(command => command.Id == id) : null;
        if (selected is null)
        {
            SelectedCommandId = null;
        }

        SelectedCommand = selected;
        var steps = selected is null ? [] : _store.FindCommand(selected.Id)!.Value.Command.Steps.Select(StepItem.From).ToList();
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
    }
}
