using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Augram.App.Components.ListEditing;

/// <summary>Where the keyboard focus goes after <see cref="RowListHost.Replace"/> when the list had it.</summary>
internal enum FocusAfterRebuild
{
    /// <summary>Nowhere: the rows keep no focus of their own (the step list: Avalonia moves it on the press).</summary>
    None,

    /// <summary>To the selected row's replacement once it is laid out (the Exclusions lists).</summary>
    SelectedRow,

    /// <summary>
    /// As <see cref="SelectedRow"/>, unless a name is being edited by then or the selected row already holds the focus (the
    /// command tree, whose header toggle rebuilds the rows under the pointer; Joel, 2026-10-07: Return renamed commands but not groups).
    /// </summary>
    SelectedRowUnlessSettled,
}

/// <summary>
/// The plumbing every editable list shares around its <c>ListBox</c> (S14b, plan 0007), owned by each list as a field, not
/// inherited: the template's list (<see cref="Attach"/>), the host's selection applied without echoing back as a user
/// selection (<see cref="SelectQuietly"/>), the rebuild that keeps selection and focus
/// (<see cref="Replace"/>), right-click selects (<see cref="SelectOnRightPress"/>), the double-click rule
/// (<see cref="RenameOnDoubleClick"/> for rows, the list's <c>DoubleTapped</c> for tiles), select-and-rename
/// (<see cref="BeginRename"/>) and the keys standing down while a name or a form is edited (<see cref="KeysEnabled"/>).
/// What differs between the lists is an option here or stays in the list: how a row is found by id, what a user selection
/// raises, which rows are reused.
/// </summary>
internal sealed class RowListHost
{
    private readonly Func<bool> _isEditing;
    private readonly FocusAfterRebuild _focus;
    private bool _applying;

    /// <param name="isEditing">True while a row's name editor (or the step list's open form) has focus.</param>
    /// <param name="focus">Where the focus goes after a rebuild of a list that had it.</param>
    public RowListHost(Func<bool> isEditing, FocusAfterRebuild focus = FocusAfterRebuild.None)
    {
        _isEditing = isEditing;
        _focus = focus;
    }

    /// <summary>The template's list; null before the template is applied or when it has none.</summary>
    public ListBox? List { get; private set; }

    public object? SelectedItem => List?.SelectedItem;

    /// <summary>True while a row's name (or a form) is being edited.</summary>
    public bool IsEditing => _isEditing();

    /// <summary>
    /// Takes the template's list. <paramref name="selectionChanged"/> runs for every selection change that is not the host's
    /// own (<see cref="SelectQuietly"/>, <see cref="Replace"/>); a list that never applies quietly hears every change. <paramref name="doubleTapped"/>, for
    /// a list of tiles, is the list's double-click action (rows read the press count instead: <see cref="RenameOnDoubleClick"/>).
    /// </summary>
    public ListBox? Attach(ListBox? list, Action selectionChanged, Action? doubleTapped = null)
    {
        ArgumentNullException.ThrowIfNull(selectionChanged);
        List = list;
        if (list is not null)
        {
            list.SelectionChanged += (_, _) =>
            {
                if (!_applying)
                {
                    selectionChanged();
                }
            };
            if (doubleTapped is not null)
            {
                list.DoubleTapped += (_, _) => doubleTapped();
            }
        }

        return list;
    }

    /// <summary>The key bindings' <c>canExecute</c>: no while a name editor or a form has focus, so Delete and the undo key edit the text.</summary>
    public bool KeysEnabled() => !_isEditing();

    /// <summary>A selection change that is not the user's: the list's SelectionChanged raises nothing meanwhile.</summary>
    private void Quietly(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);
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

    /// <summary>Selects the row the host asked for (by its id, found by the list) without raising Select back at it.</summary>
    public void SelectQuietly(object? row)
    {
        if (List is { } list)
        {
            Quietly(() => list.SelectedItem = row);
        }
    }

    /// <summary>Selects a row as the user would: the list's SelectionChanged runs.</summary>
    public void Select(object? row)
    {
        if (List is { } list)
        {
            list.SelectedItem = row;
        }
    }

    /// <summary>
    /// Replaces every row: <paramref name="setRows"/> quietly (replacing the rows clears the list's selection at once; that is
    /// not the user selecting nothing), then <paramref name="applySelection"/>, then, when the list had the keyboard focus, the
    /// focus to the selected row's replacement once it is laid out (<see cref="FocusAfterRebuild"/>): otherwise it went with
    /// the old row and the rename and delete keys stopped reaching the list.
    /// </summary>
    public void Replace(Action setRows, Action applySelection)
    {
        ArgumentNullException.ThrowIfNull(setRows);
        ArgumentNullException.ThrowIfNull(applySelection);
        var hadFocus = List?.IsKeyboardFocusWithin == true;
        Quietly(setRows);
        applySelection();
        if (hadFocus && _focus != FocusAfterRebuild.None)
        {
            Dispatcher.UIThread.Post(FocusSelectedRow, DispatcherPriority.Loaded);
        }
    }

    private void FocusSelectedRow()
    {
        if (_focus == FocusAfterRebuild.SelectedRow)
        {
            (List?.SelectedItem is { } selected ? List.ContainerFromItem(selected) : null)?.Focus();
        }
        else if (List?.SelectedItem is { } selected && !IsEditing && !(List.IsKeyboardFocusWithin && List.ContainerFromItem(selected)?.IsKeyboardFocusWithin == true))
        {
            List.ContainerFromItem(selected)?.Focus();
        }
    }

    /// <summary>Right-click selects before the context menu opens, so the menu acts on the row under the pointer. True when it was a right press.</summary>
    public bool SelectOnRightPress(Control row, PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(e);
        if (List is null || !e.GetCurrentPoint(row).Properties.IsRightButtonPressed)
        {
            return false;
        }

        List.SelectedItem = row;
        return true;
    }

    /// <summary>
    /// The double-click rule of a list of rows (<see cref="ItemRow.IsRenameClick"/>): on the second click,
    /// <paramref name="rename"/> runs once the press has been handled, so rows the first click rebuilt are settled. The count
    /// comes from the press, not from a double-tap gesture, which needs both clicks on one element. True when it renames.
    /// </summary>
    public bool RenameOnDoubleClick(ItemRow row, PointerPressedEventArgs e, Action<ItemRow> rename)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(rename);
        if (!row.IsRenameClick(e))
        {
            return false;
        }

        Dispatcher.UIThread.Post(() => rename(row));
        return true;
    }

    /// <summary>Selects the row and starts renaming it in place.</summary>
    public void BeginRename(ItemRow? row)
    {
        if (List is { } list && row is not null)
        {
            list.SelectedItem = row;
            row.BeginEdit();
        }
    }

    /// <summary>Starts renaming the selected row, without selecting anything.</summary>
    public void RenameSelected() => (List?.SelectedItem as ItemRow)?.BeginEdit();

    /// <summary>Whether any of the rows has its name editor open: the usual <c>isEditing</c>.</summary>
    public static bool AnyEditing(IEnumerable<Control> rows) => rows.Any(row => row is ItemRow { IsEditing: true });
}
