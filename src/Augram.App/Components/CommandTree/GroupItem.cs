using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// One app group of the <see cref="CommandTree"/> (F5a): its name, whether it is the pinned Global
/// group, the active flag, whether its commands are shown, and the commands themselves (always all
/// of them, sorted by the store; the tree hides them while collapsed).
/// </summary>
public sealed record GroupItem(GroupId Id, string Name, bool IsGlobal, bool IsActive, bool IsExpanded, IReadOnlyList<CommandItem> Commands)
{
    public static GroupItem From(AppGroup group, bool isExpanded, Func<GestureId, Gesture?> findGesture)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(findGesture);
        var commands = group.Commands
            .Select(command => CommandItem.From(group, command, command.Trigger is Trigger.GestureTrigger gesture ? findGesture(gesture.GestureId) : null))
            .ToList();
        return new GroupItem(group.Id, group.Name, group.IsGlobal, group.IsActive, isExpanded, commands);
    }
}
