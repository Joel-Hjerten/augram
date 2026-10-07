using Augram.App.Components.ItemList;
using Augram.App.Declarations;
using Augram.App.Navigation;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.UsedBy;

/// <summary>
/// The "Used by…" popup (F3, decision 2026-10-07): every app group › command bound to one gesture, as
/// an <see cref="ItemList"/> over a declared <see cref="ListSpec"/> so the theme styles it without a
/// theme change. The list follows the mapping store while the window is open. A row's "Go to command"
/// (toolbar, or double-click) hands the command to <see cref="ICommandLocator"/> and closes the
/// window; without a locator (no Commands tab registered) the rows are read-only. Escape closes.
/// </summary>
public sealed class UsedByWindow : Window
{
    private const string EmptyText = "No command uses this gesture.";
    private const string HelpText = "Commands bound to this gesture. Double-click a row, or select it and press Go to command, to jump to it on the Commands tab.";

    private readonly GestureId _gestureId;
    private readonly MappingStore _mapping;
    private readonly ICommandLocator? _locator;
    private readonly ListSource<UsedByRow> _source;
    private readonly ItemList _list;
    private readonly TextBlock _empty;

    public UsedByWindow(GestureId gestureId, string gestureName, MappingStore mapping, ICommandLocator? locator)
    {
        ArgumentNullException.ThrowIfNull(gestureName);
        ArgumentNullException.ThrowIfNull(mapping);
        _gestureId = gestureId;
        _mapping = mapping;
        _locator = locator;
        _source = new ListSource<UsedByRow>(() => UsedByRow.For(_mapping, _gestureId));

        Title = "Used by: " + gestureName;
        Width = 560;
        Height = 360;
        MinWidth = 400;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;

        _list = new ItemList { Spec = BuildSpec() };
        _list.DoubleTapped += (_, _) => GoTo(_list.SelectedItem as UsedByRow);
        _empty = new TextBlock { Text = EmptyText };
        _empty.Classes.Add("note");
        var help = new TextBlock { Text = HelpText };
        help.Classes.Add("help");
        var close = new Button { Content = "Close" };
        close.Classes.Add("toolbar");
        close.Click += (_, _) => Close();
        var buttons = new StackPanel();
        buttons.Classes.Add("dialog-buttons");
        buttons.Children.Add(close);

        var panel = new DockPanel();
        panel.Classes.Add("dialog");
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(help, Dock.Top);
        DockPanel.SetDock(_empty, Dock.Top);
        panel.Children.Add(buttons);
        panel.Children.Add(help);
        panel.Children.Add(_empty);
        panel.Children.Add(_list);
        Content = panel;

        _mapping.Changed += OnMappingChanged;
        Closed += (_, _) => _mapping.Changed -= OnMappingChanged;
        Opened += (_, _) => RefreshEmpty();
    }

    /// <summary>The rows on show, in document order.</summary>
    public IReadOnlyList<UsedByRow> Rows => _list.Rows.OfType<UsedByRow>().ToList();

    /// <summary>True when no command uses the gesture; the list then yields to <see cref="EmptyText"/>.</summary>
    public bool IsEmpty => _list.Rows.Count == 0;

    /// <summary>Jumps to the command through the locator and closes; a no-op without a locator or a row.</summary>
    public void GoTo(UsedByRow? row)
    {
        if (row is null || _locator is null)
        {
            return;
        }

        _locator.ShowCommand(row.CommandId);
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private ListSpec BuildSpec() => new(
        "Commands",
        Columns:
        [
            new ListColumn("App group", row => ((UsedByRow)row).Group, 150),
            new ListColumn("Command", row => ((UsedByRow)row).CommandText, 170),
            new ListColumn("Steps", row => ((UsedByRow)row).Steps),
        ],
        Source: _source,
        Toolbar: _locator is null ? null : [new ListAction("Go to command", row => GoTo(row as UsedByRow))]);

    private void OnMappingChanged(object? sender, EventArgs e)
    {
        _source.NotifyChanged();
        RefreshEmpty();
    }

    private void RefreshEmpty()
    {
        var empty = IsEmpty;
        _empty.IsVisible = empty;
        _list.IsVisible = !empty;
    }
}
