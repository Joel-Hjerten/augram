using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless tree of app groups and their commands (F5a, F7): <see cref="Groups"/> in (Global first,
/// sorted by the host), one <see cref="CommandTreeActionEventArgs"/> out per user intent. Rendered as
/// one flat list (<c>PART_Rows</c>) of <see cref="GroupRow"/>s with a <see cref="CommandRow"/> under
/// each expanded group; the toolbar buttons <c>PART_NewGroup</c>, <c>PART_NewCommand</c>,
/// <c>PART_Undo</c>, <c>PART_Redo</c>, the right-click menu and the keymap all end in that event.
/// Selection follows <see cref="SelectedCommandId"/> / <see cref="SelectedGroupId"/> from the host and
/// survives a rebuild; a user click raises <see cref="CommandTreeAction.Select"/>.
/// </summary>
public sealed class CommandTree : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<GroupItem>> GroupsProperty =
        AvaloniaProperty.Register<CommandTree, IReadOnlyList<GroupItem>>(nameof(Groups), []);

    public static readonly StyledProperty<IReadOnlyList<Control>> RowsProperty =
        AvaloniaProperty.Register<CommandTree, IReadOnlyList<Control>>(nameof(Rows), []);

    public static readonly StyledProperty<CommandId?> SelectedCommandIdProperty =
        AvaloniaProperty.Register<CommandTree, CommandId?>(nameof(SelectedCommandId));

    public static readonly StyledProperty<GroupId?> SelectedGroupIdProperty =
        AvaloniaProperty.Register<CommandTree, GroupId?>(nameof(SelectedGroupId));

    public static readonly StyledProperty<bool> CanUndoProperty =
        AvaloniaProperty.Register<CommandTree, bool>(nameof(CanUndo));

    public static readonly StyledProperty<bool> CanRedoProperty =
        AvaloniaProperty.Register<CommandTree, bool>(nameof(CanRedo));

    private ListBox? _list;
    private bool _applying;

    public event EventHandler<CommandTreeActionEventArgs>? ActionRequested;

    public IReadOnlyList<GroupItem> Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    /// <summary>The built rows, in order; the template's list shows these.</summary>
    public IReadOnlyList<Control> Rows
    {
        get => GetValue(RowsProperty);
        private set => SetValue(RowsProperty, value);
    }

    public CommandId? SelectedCommandId
    {
        get => GetValue(SelectedCommandIdProperty);
        set => SetValue(SelectedCommandIdProperty, value);
    }

    /// <summary>The selected group row, or the group of the selected command; the target of "New command".</summary>
    public GroupId? SelectedGroupId
    {
        get => GetValue(SelectedGroupIdProperty);
        set => SetValue(SelectedGroupIdProperty, value);
    }

    public bool CanUndo
    {
        get => GetValue(CanUndoProperty);
        set => SetValue(CanUndoProperty, value);
    }

    public bool CanRedo
    {
        get => GetValue(CanRedoProperty);
        set => SetValue(CanRedoProperty, value);
    }

    public CommandItem? SelectedCommand => (_list?.SelectedItem as CommandRow)?.Item;

    public GroupItem? SelectedGroup => _list?.SelectedItem switch
    {
        GroupRow row => row.Item,
        CommandRow row when row.Item is { } item => Groups.FirstOrDefault(group => group.Id == item.GroupId),
        _ => null,
    };

    /// <summary>True while a row's name is being edited in place; the key bindings stand down then.</summary>
    public bool IsEditing => Rows.Any(row => row is ItemRow { IsEditing: true });

    /// <summary>Starts in-place rename of the selected row (the rename key or the menu).</summary>
    public void BeginRename() => (_list?.SelectedItem as ItemRow)?.BeginEdit();

    /// <summary>Selects the command and starts renaming it (a freshly created "New command N").</summary>
    public void BeginRename(CommandId id)
    {
        if (_list is not null && Rows.OfType<CommandRow>().FirstOrDefault(row => row.Item?.Id == id) is { } row)
        {
            _list.SelectedItem = row;
            row.BeginEdit();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<ListBox>("PART_Rows");
        if (_list is not null)
        {
            _list.ContextMenu = CommandTreeMenu.Build(Request);
            _list.ContextMenu.Opening += (_, _) => CommandTreeMenu.Refresh(_list.ContextMenu, SelectedGroup, SelectedCommand);
            KeyBindings.Clear();
            CommandTreeMenu.BindKeys(this, CommandsKeymap.Current, Request, () => !IsEditing);
            _list.SelectionChanged += (_, _) =>
            {
                if (!_applying)
                {
                    Raise(CommandTreeAction.Select, SelectedGroup, SelectedCommand);
                }
            };
            ApplySelection();
        }

        Wire(e, "PART_NewGroup", CommandTreeAction.NewGroup);
        Wire(e, "PART_NewCommand", CommandTreeAction.NewCommand);
        Wire(e, "PART_Undo", CommandTreeAction.Undo);
        Wire(e, "PART_Redo", CommandTreeAction.Redo);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GroupsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedCommandIdProperty || change.Property == SelectedGroupIdProperty)
        {
            ApplySelection();
        }
    }

    private void Wire(TemplateAppliedEventArgs e, string part, CommandTreeAction action)
    {
        if (e.NameScope.Find<Button>(part) is { } button)
        {
            button.Click += (_, _) => Request(action);
        }
    }

    private void Rebuild()
    {
        var rows = new List<Control>();
        foreach (var group in Groups)
        {
            var header = new GroupRow { Item = group };
            header.ExpandToggled += (_, _) => Raise(CommandTreeAction.ToggleExpanded, group);
            header.RenameCommitted += (_, name) => Raise(CommandTreeAction.Rename, group, null, name);
            header.ActiveToggled += (_, _) => Raise(CommandTreeAction.ToggleActive, group);
            header.PointerPressed += OnRowPressed;
            rows.Add(header);
            if (!group.IsExpanded)
            {
                continue;
            }

            foreach (var command in group.Commands)
            {
                var row = new CommandRow { Item = command };
                row.RenameCommitted += (_, name) => Raise(CommandTreeAction.Rename, group, command, name);
                row.ActiveToggled += (_, _) => Raise(CommandTreeAction.ToggleActive, group, command);
                row.PointerPressed += OnRowPressed;
                rows.Add(row);
            }
        }

        // Replacing the rows clears the list's selection at once; that is not the user selecting nothing.
        _applying = true;
        try
        {
            Rows = rows;
        }
        finally
        {
            _applying = false;
        }

        ApplySelection();
    }

    /// <summary>Selects the row the host asked for without raising Select back at it.</summary>
    private void ApplySelection()
    {
        if (_list is null)
        {
            return;
        }

        Control? row = SelectedCommandId is { } commandId
            ? Rows.OfType<CommandRow>().FirstOrDefault(candidate => candidate.Item?.Id == commandId)
            : SelectedGroupId is { } groupId
                ? Rows.OfType<GroupRow>().FirstOrDefault(candidate => candidate.Item?.Id == groupId)
                : null;
        _applying = true;
        try
        {
            _list.SelectedItem = row;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Right-click selects before the context menu opens, so the menu acts on the row under the pointer.</summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control row && _list is not null && e.GetCurrentPoint(row).Properties.IsRightButtonPressed)
        {
            _list.SelectedItem = row;
        }
    }

    private void Request(CommandTreeAction action)
    {
        var group = SelectedGroup;
        var command = SelectedCommand;
        switch (action)
        {
            case CommandTreeAction.Rename:
                BeginRename();
                return;
            case CommandTreeAction.Delete or CommandTreeAction.ToggleActive when group is null:
            case CommandTreeAction.Copy when command is null:
            case CommandTreeAction.EditGroup when group is null or { IsGlobal: true }:
                return;
            default:
                Raise(action, group, command);
                return;
        }
    }

    private void Raise(CommandTreeAction action, GroupItem? group, CommandItem? command = null, string? name = null)
        => ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(action, group, command, name));
}
