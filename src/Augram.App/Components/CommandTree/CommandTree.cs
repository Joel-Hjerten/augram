using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Augram.App.Components.SearchBox;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
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
/// rebuild; a user click raises <see cref="CommandTreeAction.Select"/>. The toolbar's search box (<c>PART_Search</c>, plan
/// 0009) is two-way bound to <see cref="SearchText"/>, which the host narrows the sections by; the find key focuses it, the
/// edit keys stand down while it has the focus, and <see cref="EmptyText"/> says "No commands match 'xyz'" while the search
/// leaves nothing.
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

    public static readonly StyledProperty<int> PlatformFilterNudgesProperty =
        AvaloniaProperty.Register<CommandTree, int>(nameof(PlatformFilterNudges));

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<CommandTree, string?>(nameof(EmptyText));

    /// <summary>The platform filter toggle's tooltip (Joel, 0.11.9).</summary>
    public const string PlatformFilterHelp =
        "Shows or hides groups, categories and commands from the other platform (PC or Mac).";

    public static readonly StyledProperty<string> NewSectionLabelProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(NewSectionLabel), "New group");

    public static readonly StyledProperty<string> HelpTextProperty =
        AvaloniaProperty.Register<CommandTree, string>(nameof(HelpText), string.Empty);

    private readonly RowListHost _rows;
    private ToggleButton? _otherPlatforms;
    private SearchBox.SearchBox? _search;

    public CommandTree()
    {
        _rows = new RowListHost(() => IsEditing, FocusAfterRebuild.SelectedRowUnlessSettled);
    }

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

    public CommandItem? SelectedCommand => (_rows.SelectedItem as CommandRow)?.Item;

    public SectionItem? SelectedSection => _rows.SelectedItem switch
    {
        SectionRow row => row.Item,
        CommandRow row when row.Item is { } item => Sections.FirstOrDefault(section => section.Id == item.Section),
        _ => null,
    };

    /// <summary>True while a row's name is being edited in place, or the search box is typed in; the key bindings stand down then.</summary>
    public bool IsEditing => RowListHost.AnyEditing(Rows) || _search?.IsTyping == true;

    /// <summary>The search box's text (plan 0009), two-way: the host narrows <see cref="Sections"/> by it.</summary>
    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>"No commands match 'xyz'" while the search leaves no section; null otherwise (the template hides it).</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        private set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>Starts in-place rename of the selected row (the rename key or the menu).</summary>
    public void BeginRename() => _rows.RenameSelected();

    /// <summary>Selects the command and starts renaming it (a freshly created "New command N").</summary>
    public void BeginRename(CommandId id) => _rows.BeginRename(CommandTreeRows.Command(Rows, id));

    /// <summary>Selects the section and starts renaming it (a freshly created "New category N").</summary>
    public void BeginRename(SectionId id) => _rows.BeginRename(CommandTreeRows.Section(Rows, id));

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

    /// <summary>
    /// Bumped by the host when an edit hid the selected row behind the platform filter (its Use on left this platform out); each
    /// bump blinks the toggle twice (its <c>nudge</c> class on, off, on, off), so the user sees how to get the row back (Joel, 0.11.9).
    /// </summary>
    public int PlatformFilterNudges
    {
        get => GetValue(PlatformFilterNudgesProperty);
        set => SetValue(PlatformFilterNudgesProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _search = e.NameScope.Find<SearchBox.SearchBox>("PART_Search");
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

        if (_rows.Attach(e.NameScope.Find<ListBox>("PART_Rows"), () => Raise(CommandTreeAction.Select, SelectedSection, SelectedCommand)) is { } list)
        {
            list.ContextMenu = CommandTreeMenu.Build(Request);
            list.ContextMenu.Opening += (_, _) => CommandTreeMenu.Refresh(list.ContextMenu, SelectedSection, SelectedCommand, NewSectionLabel);
            KeyBindings.Clear();
            CommandTreeMenu.BindKeys(this, EditKeymap.Current, Request, _rows.KeysEnabled);
            ApplySelection();
        }

        e.OnClick("PART_NewSection", () => Request(CommandTreeAction.NewSection));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SectionsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SearchTextProperty)
        {
            ProjectEmptyText();
        }
        else if (change.Property == SelectedCommandIdProperty || change.Property == SelectedSectionIdProperty)
        {
            ApplySelection();
        }
        else if (change.Property == ShowsOtherPlatformsProperty && _otherPlatforms is not null)
        {
            _otherPlatforms.IsChecked = ShowsOtherPlatforms;
        }
        else if (change.Property == PlatformFilterNudgesProperty && change.GetNewValue<int>() > change.GetOldValue<int>())
        {
            BlinkPlatformFilter();
        }
    }

    /// <summary>On for 180 ms, off, on, off: two blinks of the toggle's <c>nudge</c> class.</summary>
    private void BlinkPlatformFilter()
    {
        if (_otherPlatforms is not { } toggle)
        {
            return;
        }

        var step = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, _) =>
        {
            step++;
            toggle.Classes.Set("nudge", step == 2);
            if (step >= 3)
            {
                timer.Stop();
            }
        };
        toggle.Classes.Set("nudge", true);
        timer.Start();
    }

    /// <summary>
    /// Replaces every row. When the list had the keyboard focus (a click on a header toggles it, which lands here), the
    /// focus moves to the selected row's replacement once it is laid out (<see cref="FocusAfterRebuild.SelectedRowUnlessSettled"/>);
    /// otherwise it went with the old row and the rename and delete keys stopped reaching the tree (Joel, 2026-10-07: Return
    /// renamed commands but not groups).
    /// </summary>
    private void Rebuild()
    {
        var rows = CommandTreeRows.Build(Sections, (action, section, command, name) => Raise(action, section, command, name), OnRowPressed);
        _rows.Replace(() => Rows = rows, ApplySelection);
        ProjectEmptyText();
    }

    private void ProjectEmptyText()
    {
        var search = SearchQuery.Parse(SearchText);
        EmptyText = Sections.Count == 0 && !search.IsEmpty ? search.NoneMatch("commands") : null;
    }

    /// <summary>
    /// Selects the row the host asked for without raising Select back at it, and shows the New command button on the header
    /// of the section the host targets (<see cref="SelectedSectionId"/>) and on no other: none while nothing is selected.
    /// </summary>
    private void ApplySelection()
    {
        if (_rows.List is not null)
        {
            _rows.SelectQuietly(CommandTreeRows.Find(Rows, SelectedSectionId, SelectedCommandId));
        }

        foreach (var header in Rows.OfType<SectionRow>())
        {
            header.ShowsNewCommand = SelectedSectionId is { } target && header.Item?.Id == target;
        }
    }

    /// <summary>
    /// Right-click selects before the context menu opens, so the menu acts on the row under the pointer. A double click
    /// on a command row's name or body renames it (Joel, 2026-10-07); on a section header only one on its name text does
    /// (Joel, 2026-10-11), elsewhere the header toggles on each click (<see cref="ItemRow.IsRenameClick"/>, the rule both
    /// follow). The count comes from the press, not from a double-tap gesture: a header's first click toggles it, which
    /// rebuilds the rows, and the gesture needs both clicks on one element.
    /// </summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || _rows.List is null)
        {
            return;
        }

        if (!_rows.SelectOnRightPress(row, e) && row is ItemRow item)
        {
            _rows.RenameOnDoubleClick(item, e, RenameByDoubleClick);
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

    /// <summary>The toolbar, menu and keys: Rename starts the editor, Find focuses the search box; anything else reaches the host only when the selection allows it.</summary>
    private void Request(CommandTreeAction action)
    {
        if (action == CommandTreeAction.Rename)
        {
            BeginRename();
        }
        else if (action == CommandTreeAction.Find)
        {
            _search?.Focus();
        }
        else if (CommandTreeMenu.Allows(action, SelectedSection, SelectedCommand))
        {
            Raise(action, SelectedSection, SelectedCommand);
        }
    }

    private void Raise(CommandTreeAction action, SectionItem? section, CommandItem? command = null, string? name = null)
        => ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(action, section, command, name));
}
