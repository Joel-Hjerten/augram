using Augram.App.Components.CommandTree;
using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.UsedBy;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The two lists half of <see cref="IgnoredViewModel"/> (plan 0004, decisions 5 and 7): an Exclusions › Per command entry's users,
/// the commands whose "Not in" names it (its row's summary, its form's "Used by" links that open each command, the delete and
/// move questions), and the right-click menu's move between Global and Per command. A move is one <c>UpdateIgnored</c>, one
/// undo step: moving to Global, Core's normalisation drops the entry from every command's "Not in" in the same step, so the
/// user is asked first when a command names it; moving to Per command, a "Disable while focused" mode is dropped the same way.
/// </summary>
public sealed partial class IgnoredViewModel
{
    private async Task MoveAsync(MasterItem item)
    {
        var app = Require(item.Id);
        var users = UsersOf(app.Id);
        if (app.IsPerCommand && users.Count > 0
            && !await _confirm.ConfirmAsync(
                "Move to Global",
                $"'{app.Name}' is used by {Commands(users.Count)}; on Global it stops all of Augram over {app.Name} and leaves their Not in. Move?",
                "Move").ConfigureAwait(true))
        {
            return;
        }

        if (!app.IsPerCommand && AllowedUsersOf(app.Id) is { Count: > 0 } allowed
            && !await _confirm.ConfirmAsync("Move to Per command", MoveAllowedQuestion(app, allowed.Count), "Move").ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var current = Require(item.Id);
            var moved = _store.UpdateIgnored(current with { Scope = current.IsPerCommand ? IgnoreScope.Global : IgnoreScope.PerCommand });
            var undo = $"{CommandsKeymap.Current.Undo} undoes it.";
            Message = moved.IsPerCommand
                ? $"Moved '{moved.Name}' to Exclusions › Per command: it stops nothing until a command names it in its Not in{(current.DisableEntirely ? ", and it no longer disables Augram while focused" : string.Empty)}. {undo}"
                : $"Moved '{moved.Name}' to Exclusions › Global: Augram stays out of it, hold remaps aside. {undo}";
        });
    }

    /// <summary>The commands whose "Not in" names the entry, in document order (Global first, then the app groups by name).</summary>
    private List<(AppGroup Group, Command Command)> UsersOf(GroupId id)
        => [.. _store.Current.AllCommands().Where(pair => pair.Command.NotIn.Contains(id))];

    /// <summary>"Used by 2 commands", "Not used yet": the row summary's first part for a Per command entry.</summary>
    private string UsersText(GroupId id) => UsersOf(id).Count is var count and > 0 ? $"Used by {Commands(count)}" : "Not used yet";

    /// <summary>" The 2 commands naming it in their Not in work over it again.", or nothing for an entry no command names.</summary>
    private string UsersSentence(GroupId id) => UsersOf(id).Count switch
    {
        0 => string.Empty,
        1 => " The command naming it in its Not in works over it again.",
        var count => $" The {count} commands naming it in their Not in work over it again.",
    };

    /// <summary>
    /// The entry's "Used by" as links, "Global › Media › Zoom in" (<see cref="UsedByRow.Label"/>, the Gestures tab's words), each
    /// opening its command on the Commands tab when there is a locator, "inactive" after a command that is switched off.
    /// </summary>
    private List<LinkItem> UsedByLinks(GroupId id) => Links(UsersOf(id));

    /// <summary>Each command as a link, "Global › Media › Zoom in", opening it on the Commands tab when there is a locator; "inactive" after one switched off.</summary>
    private List<LinkItem> Links(IEnumerable<(AppGroup Group, Command Command)> users)
        => [.. users.Select(pair =>
        {
            var commandId = pair.Command.Id;
            return new LinkItem(
                UsedByRow.From(pair.Group, pair.Command).Label,
                _commands is { } commands ? () => commands.ShowCommand(commandId) : null,
                pair.Command.IsActive ? null : "inactive");
        })];

    private static string Commands(int count) => count == 1 ? "1 command" : $"{count} commands";
}
