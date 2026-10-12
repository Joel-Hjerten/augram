using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The Commands tab's lookups of an item an intent acts on, shared by <see cref="CommandsViewModel"/> and its collaborators:
/// the item, or a <see cref="KeyNotFoundException"/> with the words the message line shows (<c>Guard</c> treats it as a
/// refusal: the item went meanwhile, an undo or a sync).
/// </summary>
internal static class CommandLookups
{
    public static (AppGroup Group, Command Command) RequireCommand(this MappingStore store, CommandId id)
        => store.FindCommand(id) ?? throw new KeyNotFoundException("That command no longer exists.");

    public static AppGroup RequireGroup(this MappingStore store, GroupId id)
        => store.FindGroup(id) ?? throw new KeyNotFoundException("That app group no longer exists.");

    public static HoldRemap RequireHoldRemap(this MappingStore store, GroupId groupId, HoldRemapId id)
        => store.RequireGroup(groupId).FindHoldRemap(id) ?? throw new KeyNotFoundException("That hold remap no longer exists.");
}
