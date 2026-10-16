using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The side panel half of <see cref="IgnoredViewModel"/>, the same pattern as the Apps tab's group panel
/// (<see cref="PanelEdit{TEdit, TId}"/>): the selected app's form (<see cref="Detail"/>, an <see cref="IgnoredEditViewModel"/>),
/// rebuilt only when another app is selected so typing never loses focus. Each edit applies at once, one undo step each; a rule
/// the store refuses (an empty name, a pattern that does not compile) shows on the message line and the field keeps what was
/// typed. A change from elsewhere (undo, a rename or the active box in the list, a sync) is synced into the form, but the form's
/// own edits are not echoed back. A Per command entry's "Used by" (<see cref="IgnoredEditViewModel.UsedBy"/>) and a Global
/// entry's "Allowed for" (<see cref="IgnoredEditViewModel.AllowedFor"/>) are re-read on every store change, a command's Not in
/// or Also in included.
/// </summary>
public sealed partial class IgnoredViewModel
{
    private PanelEdit<IgnoredEditViewModel, GroupId>? _panel;

    // GuessText is computed from the fields; its notice follows a real edit that is applied already. UsedBy, AllowedFor and GlobalNote are the host's.
    private PanelEdit<IgnoredEditViewModel, GroupId> Panel => _panel ??= new(
        OnEdited,
        nameof(AppMatcherEditViewModel.GuessText),
        nameof(IgnoredEditViewModel.UsedBy),
        nameof(IgnoredEditViewModel.AllowedFor),
        nameof(IgnoredEditViewModel.GlobalNote));

    /// <summary>The selected app's form; null while nothing is selected.</summary>
    [ObservableProperty]
    public partial FormScreen? Detail { get; private set; }

    private void ProjectDetail()
    {
        var app = SelectedId is { } id ? _store.FindIgnored(new GroupId(id)) : null;
        ProjectHeldInputs(app);
        if (app is null)
        {
            Panel.Close();
            Detail = null;
            return;
        }

        var shown = Panel.Show(
            app.Id,
            () =>
            {
                var edit = IgnoredEditViewModel.From(app);
                Refresh(edit, app);
                var allowedId = app.Id;
                edit.EditAllowedFor = app.IsGlobal ? () => _ = EditAllowedForAsync(allowedId) : null;
                return edit;
            },
            edit => edit.SyncFrom(app),
            edit => Refresh(edit, app));
        if (shown is not null)
        {
            Detail = shown.Declare();
        }
    }

    /// <summary>
    /// What the host fills in the form, never an edit, re-read on every store change: a command ticking or unticking the entry in
    /// its Not in (or Also in) changes the links; a Global entry for the same app changes a While holding entry's note (plan 0012
    /// decision 9).
    /// </summary>
    private void Refresh(IgnoredEditViewModel edit, IgnoredApp app)
    {
        edit.UsedBy = app.IsPerCommand ? UsedByLinks(app.Id) : [];
        edit.AllowedFor = app.IsGlobal ? AllowedForLinks(app.Id) : [];
        edit.GlobalNote = app.IsWhileHolding ? GlobalNote(app) : null;
    }

    /// <summary>
    /// "'Blender' on Exclusions › Global names this app too: gestures pass through there anyway; this list adds only its hold
    /// remaps.", for a While holding entry whose app an active Global entry also names on this platform (plan 0012 decision 9;
    /// the hold-key conflict's matcher comparison, <see cref="AppMatcher.SharesExecutableWith"/>); null otherwise.
    /// </summary>
    private string? GlobalNote(IgnoredApp app)
        => _store.Current.Ignored.FirstOrDefault(other => other.IsGlobal && other.IsActive && other.Matcher.SharesExecutableWith(app.Matcher, _platform)) is { } global
            ? $"'{global.Name}' on Exclusions › Global names this app too: gestures pass through there anyway; this list adds only its hold remaps."
            : null;

    private void OnEdited(IgnoredEditViewModel edit, GroupId id, string? property)
    {
        Message = null;
        Guard(() =>
        {
            var allowed = UsersOf(id).Count;
            Message = LeftAlsoIn(_store.UpdateIgnored(edit.Apply(Require(id.Value))), allowed);
        });
    }
}
