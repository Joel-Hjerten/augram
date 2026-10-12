using System.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The side panel's form for the selected item, one lifecycle for all four (the Apps tab's app group and hold remap, the Global
/// tab's category, the selected excluded app): the edit view model and the id it edits. <see cref="Show"/> builds a new form
/// <b>only when another item is selected</b>, so typing never loses focus or caret; for the same item it syncs the stored values
/// into the open form (undo, a rename in the tree, a sync), except while the form's own edit is being applied, which is not
/// echoed back. A property change of the form (not one in its ignore list, not one a sync made) calls the host's
/// <c>edited</c> with the id, inside the applying window; the host clears the message, stores the edit through
/// <c>Guard</c> and, when the store refuses a value the field cannot keep, puts it back with <see cref="Sync"/>.
/// </summary>
/// <typeparam name="TEdit">The form's edit view model.</typeparam>
/// <typeparam name="TId">What identifies the item it edits (a group's id, a group and a hold remap's).</typeparam>
public sealed class PanelEdit<TEdit, TId>
    where TEdit : class, INotifyPropertyChanged
    where TId : notnull
{
    private readonly Action<TEdit, TId, string?> _edited;
    private readonly IReadOnlySet<string> _ignored;
    private TId? _id;
    private bool _applying;
    private bool _syncing;

    /// <param name="edited">Called with the form, the item's id and the changed property's name when the user edits the form.</param>
    /// <param name="ignored">Properties the form raises that are not the user's edits (a computed guess, lists the host fills).</param>
    public PanelEdit(Action<TEdit, TId, string?> edited, params string[] ignored)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(ignored);
        _edited = edited;
        _ignored = new HashSet<string>(ignored, StringComparer.Ordinal);
    }

    /// <summary>The open form; null while none is shown.</summary>
    public TEdit? Edit { get; private set; }

    /// <summary>
    /// Shows the item's form: a new one from <paramref name="create"/> when none is open or it is another item's (returned, for
    /// the host to declare), else null after <paramref name="sync"/> took the stored values (skipped while the form's own edit
    /// is applied) and <paramref name="refresh"/> what the host fills in the form (always, as a sync).
    /// </summary>
    public TEdit? Show(TId id, Func<TEdit> create, Action<TEdit> sync, Action<TEdit>? refresh = null)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(sync);
        if (Edit is null || !EqualityComparer<TId>.Default.Equals(_id, id))
        {
            Close();
            var edit = create();
            Edit = edit;
            _id = id;
            edit.PropertyChanged += OnEdited;
            return edit;
        }

        if (!_applying || refresh is not null)
        {
            Sync(edit =>
            {
                if (!_applying)
                {
                    sync(edit);
                }

                refresh?.Invoke(edit);
            });
        }

        return null;
    }

    /// <summary>Runs <paramref name="sync"/> on the open form without its changes counting as edits; nothing when none is open.</summary>
    public void Sync(Action<TEdit> sync)
    {
        ArgumentNullException.ThrowIfNull(sync);
        if (Edit is not { } edit)
        {
            return;
        }

        _syncing = true;
        try
        {
            sync(edit);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Stops following the open form and forgets it.</summary>
    public void Close()
    {
        if (Edit is not null)
        {
            Edit.PropertyChanged -= OnEdited;
        }

        Edit = null;
        _id = default;
    }

    private void OnEdited(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncing || (e.PropertyName is { } name && _ignored.Contains(name)) || Edit is not { } edit || _id is not { } id)
        {
            return;
        }

        _applying = true;
        try
        {
            _edited(edit, id, e.PropertyName);
        }
        finally
        {
            _applying = false;
        }
    }
}
