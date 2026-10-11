using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless tree of collapsible sections and their commands (F5a, F7): <see cref="Sections"/> in (sorted
/// by the host: app groups on the Apps tab, categories on the Global tab), one
/// <see cref="CommandTreeActionEventArgs"/> out per user intent. Rendered as one flat list
/// (<c>PART_Rows</c>, built by <see cref="CommandTreeRows"/>) of <see cref="SectionRow"/>s with a
/// <see cref="CommandRow"/> under each expanded section; the toolbar button <c>PART_NewSection</c>
/// (labelled <see cref="NewSectionLabel"/>), the New command button on the header of the section
/// <see cref="SelectedSectionId"/> names (<see cref="SectionRow.ShowsNewCommand"/>; no longer on the toolbar since Joel,
/// 2026-10-11: a section is chosen first, then a command made in it), the right-click menu and the keymap
/// (undo and redo are keys only since 2026-10-07: deletes ask first) all end in that event. It never asks which tab it is on: each
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

    public static readonly StyledProperty<string> HeadingProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(Heading), "Commands");

    public static readonly StyledProperty<string?> PlatformFilterLabelProperty =
        AvaloniaProperty.Register<CommandTree, string?>(nameof(PlatformFilterLabel));

    public static readonly StyledProperty<bool> ShowsOtherPlatformsProperty =
        AvaloniaProperty.Register<CommandTree, bool>(nameof(ShowsOtherPlatforms));

    public static readonly StyledProperty<string> NewSectionLabelProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(NewSectionLabel), "New group");

    public static readonly StyledProperty<string> HelpTextProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(HelpText), string.Empty);

    private ToggleButton? _otherPlatforms;
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

    /// <summary>The selected section row, or the section of the selected command; the target of "New command" and Paste, whose header shows the New command button.</summary>
    public SectionId? SelectedSectionId
    {
        get => GetValue(SelectedSectionIdProperty);
        set => SetValue(SelectedSectionIdProperty, value);
    }

    /// <summary>The title above the toolbar ("Global commands", "App groups").</summary>
    public string Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>The words on the new-section button and menu entry ("New group", "New category").</summary>
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

    /// <summary>The toolbar toggle's label ("Show other platforms"); null hides it (the Global tab has no platform filter).</summary>
    public string? PlatformFilterLabel
    {
        get => GetValue(PlatformFilterLabelProperty);
        set => SetValue(PlatformFilterLabelProperty, value);
    }

    /// <summary>The host shows the groups used only on the other platform; the toggle mirrors it and asks the host to flip it.</summary>
    public bool ShowsOtherPlatforms
    {
        get => GetValue(ShowsOtherPlatformsProperty);
        set => SetValue(ShowsOtherPlatformsProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _otherPlatforms = e.NameScope.Find<ToggleButton>("PART_OtherPlatforms");
        if (_otherPlatforms is not null)
        {
            _otherPlatforms.IsChecked = ShowsOtherPlatforms;
            _otherPlatforms.Click += (_, _) =>
            {
                // The host owns the state: put the box back and ask; the answer arrives as ShowsOtherPlatforms.
                _otherPlatforms.IsChecked = ShowsOtherPlatforms;
                Raise(CommandTreeAction.ToggleOtherPlatforms, SelectedSection);
            };
        }

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
        else if (change.Property == ShowsOtherPlatformsProperty && _otherPlatforms is not null)
        {
            _otherPlatforms.IsChecked = ShowsOtherPlatforms;
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

    /// <summary>
    /// Replaces every row. When the list had the keyboard focus (a click on a header toggles it, which lands here), the
    /// focus moves to the selected row's replacement once it is laid out; otherwise it went with the old row and the
    /// rename and delete keys stopped reaching the tree (Joel, 2026-10-07: Return renamed commands but not groups).
    /// </summary>
    private void Rebuild()
    {
        var hadFocus = _list?.IsKeyboardFocusWithin == true;
        var rows = CommandTreeRows.Build(Sections, (action, section, command, name) => Raise(action, section, command, name), OnRowPressed);

        // Replacing the rows clears the list's selection at once; that is not the user selecting nothing.
        Quietly(() => Rows = rows);
        ApplySelection();
        if (hadFocus)
        {
            Dispatcher.UIThread.Post(FocusSelectedRow, DispatcherPriority.Loaded);
        }
    }

    private void FocusSelectedRow()
    {
        if (_list?.SelectedItem is { } selected && !IsEditing && !(_list.IsKeyboardFocusWithin && _list.ContainerFromItem(selected)?.IsKeyboardFocusWithin == true))
        {
            _list.ContainerFromItem(selected)?.Focus();
        }
    }

    /// <summary>
    /// Selects the row the host asked for without raising Select back at it, and shows the New command button on the header
    /// of the section the host targets (<see cref="SelectedSectionId"/>) and on no other: none while nothing is selected.
    /// </summary>
    private void ApplySelection()
    {
        if (_list is not null)
        {
            Quietly(() => _list.SelectedItem = CommandTreeRows.Find(Rows, SelectedSectionId, SelectedCommandId));
        }

        foreach (var header in Rows.OfType<SectionRow>())
        {
            header.ShowsNewCommand = SelectedSectionId is { } target && header.Item?.Id == target;
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

    /// <summary>
    /// Right-click selects before the context menu opens, so the menu acts on the row under the pointer. A double click
    /// on a row's name or body renames it (Joel, 2026-10-07). The count comes from the press, not from a double-tap
    /// gesture: a header's first click toggles it, which rebuilds the rows, and the gesture needs both clicks on one element.
    /// </summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || _list is null)
        {
            return;
        }

        var properties = e.GetCurrentPoint(row).Properties;
        if (properties.IsRightButtonPressed)
        {
            _list.SelectedItem = row;
        }
        else if (properties.IsLeftButtonPressed && e.ClickCount == 2 && row is ItemRow { CanRename: true, IsEditing: false } item && !item.IsFromOwnControl(e.Source as Visual))
        {
            Dispatcher.UIThread.Post(() => RenameByDoubleClick(item));
        }
    }

    /// <summary>After the press has been handled, so the rows the first click rebuilt are settled.</summary>
    private void RenameByDoubleClick(ItemRow row)
    {
        switch (row)
        {
            case CommandRow { Item: { } command }:
                BeginRename(command.Id);
                break;
            case SectionRow { Item: { } section }:
                // The double click's first click toggled the header; put it back, so a rename leaves it as it was.
                Raise(CommandTreeAction.ToggleExpanded, section);
                Dispatcher.UIThread.Post(() => BeginRename(section.Id));
                break;
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
