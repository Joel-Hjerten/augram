using System.ComponentModel;
using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The side panel half of <see cref="IgnoredViewModel"/>, the same pattern as the Apps tab's group panel: the selected app's
/// form (<see cref="Detail"/>, an <see cref="IgnoredEditViewModel"/>), rebuilt only when another app is selected so typing
/// never loses focus. Each edit applies at once, one undo step each; a rule the store refuses (an empty name, a pattern that
/// does not compile) shows on the message line and the field keeps what was typed. A change from elsewhere (undo, a rename
/// or the active box in the list, a sync) is synced into the form, but the form's own edits are not echoed back. A Per command
/// entry's "Used by" (<see cref="IgnoredEditViewModel.UsedBy"/>) and a Global entry's "Allowed for"
/// (<see cref="IgnoredEditViewModel.AllowedFor"/>) are re-read on every store change, a command's Not in or Also in included.
/// </summary>
public sealed partial class IgnoredViewModel
{
    private IgnoredEditViewModel? _edit;
    private GroupId? _editId;
    private bool _applyingEdit;
    private bool _syncingEdit;

    /// <summary>The selected app's form; null while nothing is selected.</summary>
    [ObservableProperty]
    public partial FormScreen? Detail { get; private set; }

    private void ProjectDetail()
    {
        var app = SelectedId is { } id ? _store.FindIgnored(new GroupId(id)) : null;
        if (app is null)
        {
            DetachEdit();
            Detail = null;
            return;
        }

        if (_edit is null || _editId != app.Id)
        {
            DetachEdit();
            _edit = IgnoredEditViewModel.From(app);
            _editId = app.Id;
            _edit.UsedBy = app.IsPerCommand ? UsedByLinks(app.Id) : [];
            _edit.AllowedFor = app.IsPerCommand ? [] : AllowedForLinks(app.Id);
            _edit.PropertyChanged += OnEdited;
            Detail = _edit.Declare();
        }
        else
        {
            _syncingEdit = true;
            try
            {
                if (!_applyingEdit)
                {
                    _edit.SyncFrom(app);
                }

                // A command ticking or unticking the entry in its Not in (or Also in) changes these, never the form.
                _edit.UsedBy = app.IsPerCommand ? UsedByLinks(app.Id) : [];
                _edit.AllowedFor = app.IsPerCommand ? [] : AllowedForLinks(app.Id);
            }
            finally
            {
                _syncingEdit = false;
            }
        }
    }

    private void OnEdited(object? sender, PropertyChangedEventArgs e)
    {
        // GuessText is computed from the fields; its notice follows a real edit that is applied already. UsedBy and AllowedFor are the host's.
        if (_syncingEdit
            || e.PropertyName is nameof(AppMatcherEditViewModel.GuessText) or nameof(IgnoredEditViewModel.UsedBy) or nameof(IgnoredEditViewModel.AllowedFor)
            || _edit is not { } edit
            || _editId is not { } id)
        {
            return;
        }

        Message = null;
        _applyingEdit = true;
        try
        {
            Guard(() =>
            {
                var allowed = AllowedUsersOf(id).Count;
                Message = LeftAlsoIn(_store.UpdateIgnored(edit.Apply(Require(id.Value))), allowed);
            });
        }
        finally
        {
            _applyingEdit = false;
        }
    }

    private void DetachEdit()
    {
        if (_edit is not null)
        {
            _edit.PropertyChanged -= OnEdited;
        }

        _edit = null;
        _editId = null;
    }
}
