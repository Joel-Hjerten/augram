using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless tree of collapsible sections and their commands (F5a, F7): <see cref="Sections"/> in (sorted
/// by the host: app groups on the Apps tab, categories on the Global tab), one
/// <see cref="CommandTreeActionEventArgs"/> out per user intent. Rendered as one flat list
/// (<c>PART_Rows</c>, built by <see cref="CommandTreeRows"/>) of <see cref="SectionRow"/>s with a
/// <see cref="CommandRow"/> under each expanded section; the toolbar buttons <c>PART_NewSection</c>
/// (labelled <see cref="NewSectionLabel"/>), <c>PART_NewCommand</c>, <c>PART_Undo</c>, <c>PART_Redo</c>,
/// the right-click menu and the keymap all end in that event. It never asks which tab it is on: each
/// <see cref="SectionItem"/> says what its header can do and the host supplies the words. Selection
/// follows <see cref="SelectedCommandId"/> / <see cref="SelectedSectionId"/> from the host and survives a
/// rebuild; a user click raises <see cref="CommandTreeAction.Select"/>.
/// </summary>
public sealed class CommandTree : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<SectionItem>> SectionsProperty =
        AvaloniaProperty.Register<CommandTree, IReadOnlyList<SectionItem>>(nameof(Sections), []);

    public static readonly StyledProperty<IReadOnlyList<Control>> RowsProperty =
        AvaloniaProperty.Register<CommandTree, IReadOnlyList<Control>>(nameof(Rows), []);

    public static readonly StyledProperty<CommandId?> SelectedCommandIdProperty =
        AvaloniaProperty.Register<CommandTree, CommandId?>(nameof(SelectedCommandId));

    public static readonly StyledProperty<SectionId?> SelectedSectionIdProperty =
        AvaloniaProperty.Register<CommandTree, SectionId?>(nameof(SelectedSectionId));

    public static readonly StyledProperty<bool> CanUndoProperty =
        AvaloniaProperty.Register<CommandTree, bool>(nameof(CanUndo));

    public static readonly StyledProperty<bool> CanRedoProperty =
        AvaloniaProperty.Register<CommandTree, bool>(nameof(CanRedo));

    public static readonly StyledProperty<string> HeadingProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(Heading), "Commands");

    public static readonly StyledProperty<string> NewSectionLabelProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(NewSectionLabel), "New group…");

    public static readonly StyledProperty<string> HelpTextProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(HelpText), string.Empty);

    private ListBox? _list;
    private bool _applying;

    public event EventHandler<CommandTreeActionEventArgs>? ActionRequested;

    public IReadOnlyList<SectionItem> Sections
    {
        get => GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
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

    /// <summary>The selected section row, or the section of the selected command; the target of "New command" and Paste.</summary>
    public SectionId? SelectedSectionId
    {
        get => GetValue(SelectedSectionIdProperty);
        set => SetValue(SelectedSectionIdProperty, value);
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

    /// <summary>The title above the toolbar ("Global commands", "App groups").</summary>
    public string Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>The words on the new-section button and menu entry ("New group…", "New category…").</summary>
    public string NewSectionLabel
    {
        get => GetValue(NewSectionLabelProperty);
        set => SetValue(NewSectionLabelProperty, value);
    }

    /// <summary>The help line under the list.</summary>
    public string HelpText
    {
        get => GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    public CommandItem? SelectedCommand => (_list?.SelectedItem as CommandRow)?.Item;

    public SectionItem? SelectedSection => _list?.SelectedItem switch
    {
        SectionRow row => row.Item,
        CommandRow row when row.Item is { } item => Sections.FirstOrDefault(section => section.Id == item.Section),
        _ => null,
    };

    /// <summary>True while a row's name is being edited in place; the key bindings stand down then.</summary>
    public bool IsEditing => Rows.Any(row => row is ItemRow { IsEditing: true });

    /// <summary>Starts in-place rename of the selected row (the rename key or the menu).</summary>
    public void BeginRename() => (_list?.SelectedItem as ItemRow)?.BeginEdit();

    /// <summary>Selects the command and starts renaming it (a freshly created "New command N").</summary>
    public void BeginRename(CommandId id) => BeginRename(CommandTreeRows.Command(Rows, id));

    /// <summary>Selects the section and starts renaming it (a freshly created "New category N").</summary>
    public void BeginRename(SectionId id) => BeginRename(CommandTreeRows.Section(Rows, id));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<ListBox>("PART_Rows");
        if (_list is not null)
        {
            _list.ContextMenu = CommandTreeMenu.Build(Request);
            _list.ContextMenu.Opening += (_, _) => CommandTreeMenu.Refresh(_list.ContextMenu, SelectedSection, SelectedCommand, NewSectionLabel);
            KeyBindings.Clear();
            CommandTreeMenu.BindKeys(this, CommandsKeymap.Current, Request, () => !IsEditing);
            _list.SelectionChanged += (_, _) =>
            {
                if (!_applying)
                {
                    Raise(CommandTreeAction.Select, SelectedSection, SelectedCommand);
                }
            };
            ApplySelection();
        }

        Wire(e, "PART_NewSection", CommandTreeAction.NewSection);
        Wire(e, "PART_NewCommand", CommandTreeAction.NewCommand);
        Wire(e, "PART_Undo", CommandTreeAction.Undo);
        Wire(e, "PART_Redo", CommandTreeAction.Redo);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SectionsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedCommandIdProperty || change.Property == SelectedSectionIdProperty)
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

    private void BeginRename(ItemRow? row)
    {
        if (_list is not null && row is not null)
        {
            _list.SelectedItem = row;
            row.BeginEdit();
        }
    }

    private void Rebuild()
    {
        var rows = CommandTreeRows.Build(Sections, (action, section, command, name) => Raise(action, section, command, name), OnRowPressed);

        // Replacing the rows clears the list's selection at once; that is not the user selecting nothing.
        Quietly(() => Rows = rows);
        ApplySelection();
    }

    /// <summary>Selects the row the host asked for without raising Select back at it.</summary>
    private void ApplySelection()
    {
        if (_list is not null)
        {
            Quietly(() => _list.SelectedItem = CommandTreeRows.Find(Rows, SelectedSectionId, SelectedCommandId));
        }
    }

    /// <summary>A selection change that is not the user's: the list's SelectionChanged raises nothing meanwhile.</summary>
    private void Quietly(Action change)
    {
        _applying = true;
        try
        {
            change();
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

    /// <summary>The toolbar, menu and keys: Rename starts the editor; anything else reaches the host only when the selection allows it.</summary>
    private void Request(CommandTreeAction action)
    {
        if (action == CommandTreeAction.Rename)
        {
            BeginRename();
        }
        else if (CommandTreeMenu.Allows(action, SelectedSection, SelectedCommand))
        {
            Raise(action, SelectedSection, SelectedCommand);
        }
    }

    private void Raise(CommandTreeAction action, SectionItem? section, CommandItem? command = null, string? name = null)
        => ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(action, section, command, name));
}
