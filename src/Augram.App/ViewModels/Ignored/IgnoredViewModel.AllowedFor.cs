using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The "Allowed for" half of <see cref="IgnoredViewModel"/> (plan 0005 decision 7): an Exclusions › Global entry's users, the
/// commands whose "Also in" names it, as a Per command entry's are the commands whose "Not in" names it. Its form shows them as
/// links that open each command (<see cref="IgnoredEditViewModel.AllowedFor"/>). Core's normalisation drops the entry from their
/// Also in when it stops being a plain Global entry: a move to Per command asks first when commands name it, and a switch to
/// "Disable while focused" on the form says so on the message line (one undo step brings both back).
/// </summary>
public sealed partial class IgnoredViewModel
{
    /// <summary>The commands whose "Also in" names the entry, in document order (Global first, then the app groups by name).</summary>
    private List<(AppGroup Group, Command Command)> AllowedUsersOf(GroupId id)
        => [.. _store.Current.AllCommands().Where(pair => pair.Command.AlsoIn.Contains(id))];

    /// <summary>The entry's "Allowed for" as links, "Global › Media › Magnifier", each opening its command (the Per command entries' "Used by" words).</summary>
    private List<LinkItem> AllowedForLinks(GroupId id) => Links(AllowedUsersOf(id));

    /// <summary>
    /// The Allowed for row's Change… (Joel, 2026-10-11: from the excluded app's side too): the commands that can work over an
    /// excluded app as a check list (<see cref="AllowedForEditViewModel"/>); Save stores what is ticked as one
    /// <see cref="MappingStore.SetAllowedFor"/>, one undo step, and nothing when nothing changed. Cancel stores nothing.
    /// </summary>
    private async Task EditAllowedForAsync(GroupId id)
    {
        if (_store.FindIgnored(id) is not { IsPerCommand: false, DisableEntirely: false } app)
        {
            return;
        }

        var edit = AllowedForEditViewModel.For(app, _store.Current, _platform);
        if (!await _dialogs.ShowAsync(new FormDialogRequest(AllowedForEditViewModel.Title, AllowedForEditViewModel.ConfirmLabel, Screen: edit.Declare())).ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            if (_store.SetAllowedFor(id, edit.Allowed) > 0 && _store.FindIgnored(id) is { } stored)
            {
                Message = $"'{stored.Name}' is allowed for {Commands(AllowedUsersOf(id).Count)} now. {CommandsKeymap.Current.Undo} undoes it.";
            }
        });
    }

    /// <summary>"'Blender' is allowed for 1 command; on Per command it leaves their Also in, and Augram works over Blender again. Move?"</summary>
    private static string MoveAllowedQuestion(IgnoredApp app, int count)
        => $"'{app.Name}' is allowed for {Commands(count)}; on Per command it leaves their Also in, and Augram works over {app.Name} again. Move?";

    /// <summary>
    /// What the message line says after a form edit that took the entry out of <paramref name="before"/> commands' Also in (the
    /// switch to "Disable while focused"); null when it left none.
    /// </summary>
    private string? LeftAlsoIn(IgnoredApp stored, int before)
    {
        var left = before - AllowedUsersOf(stored.Id).Count;
        return left <= 0 ? null : $"'{stored.Name}' disables Augram while focused now, so it left the Also in of {Commands(left)}. {CommandsKeymap.Current.Undo} undoes it.";
    }
}
