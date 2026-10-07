using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Pure policy for bringing an imported mapping into an existing one (F8: import merges rather than
/// replaces). <see cref="Rebind"/> points the imported commands at the gesture ids the gesture merge
/// settled on; <see cref="Merge"/> is add-only in this slice: groups merge by name (case-insensitive,
/// Global always into Global; an existing group keeps its own matcher and flags), a command whose name
/// or bound trigger is already taken in its group is skipped and counted, ignored apps merge by name.
/// Categories merge by name too: an added command lands in the existing category of the same name, or
/// brings its category along when the group lacks it. A per-group replace/overwrite choice is a later
/// slice. No UI, no store.
/// </summary>
public static class MappingImport
{
    /// <summary>Replaces every gesture trigger whose id is in <paramref name="idMap"/> (imported id → final id); other triggers are untouched.</summary>
    public static MappingDocument Rebind(MappingDocument imported, IReadOnlyDictionary<GestureId, GestureId> idMap)
    {
        ArgumentNullException.ThrowIfNull(imported);
        ArgumentNullException.ThrowIfNull(idMap);
        var groups = imported.Groups
            .Select(group => group with { Commands = group.Commands.Select(command => Rebound(command, idMap)).ToArray() })
            .ToArray();
        return imported with { Groups = groups };
    }

    public static MappingMergeResult Merge(MappingDocument existing, MappingDocument imported)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(imported);
        var groups = existing.Groups.ToList();
        if (!groups.Any(group => group.IsGlobal))
        {
            groups.Insert(0, AppGroup.EmptyGlobal);
        }

        var groupsAdded = 0;
        var commandsAdded = 0;
        var commandsSkipped = 0;
        foreach (var group in imported.Groups)
        {
            var index = group.IsGlobal
                ? groups.FindIndex(candidate => candidate.IsGlobal)
                : groups.FindIndex(candidate => !candidate.IsGlobal && MappingRules.NameComparer.Equals(candidate.Name, group.Name));
            if (index < 0)
            {
                groups.Add(group);
                groupsAdded++;
                commandsAdded += group.Commands.Count;
                continue;
            }

            var target = groups[index];
            var commands = target.Commands.ToList();
            var categories = target.Categories.ToList();
            foreach (var command in group.Commands)
            {
                if (IsTaken(command, commands))
                {
                    commandsSkipped++;
                    continue;
                }

                commands.Add(command with { CategoryId = Placed(command.CategoryId, group, categories) });
                commandsAdded++;
            }

            groups[index] = target with { Commands = commands, Categories = categories };
        }

        var ignored = existing.Ignored.ToList();
        var ignoredAdded = 0;
        var ignoredSkipped = 0;
        foreach (var app in imported.Ignored)
        {
            if (ignored.Any(candidate => MappingRules.NameComparer.Equals(candidate.Name, app.Name)))
            {
                ignoredSkipped++;
                continue;
            }

            ignored.Add(app);
            ignoredAdded++;
        }

        var document = MappingRules.ValidDocument(new MappingDocument(groups, ignored));
        return new MappingMergeResult(document, groupsAdded, commandsAdded, commandsSkipped, ignoredAdded, ignoredSkipped);
    }

    /// <summary>
    /// The id, among the merged group's <paramref name="categories"/>, of the category named like the
    /// command's category in <paramref name="imported"/>: an existing one is reused, a missing one is
    /// added (with a fresh id if its own is taken). Only categories an added command uses arrive.
    /// </summary>
    private static CategoryId? Placed(CategoryId? id, AppGroup imported, List<CommandCategory> categories)
    {
        if (id is not { } value || imported.FindCategory(value) is not { } category)
        {
            return null;
        }

        var existing = categories.FirstOrDefault(candidate => MappingRules.NameComparer.Equals(candidate.Name, category.Name));
        if (existing is not null)
        {
            return existing.Id;
        }

        var added = categories.Any(candidate => candidate.Id == category.Id) ? category with { Id = CategoryId.New() } : category;
        categories.Add(added);
        return added.Id;
    }

    private static bool IsTaken(Command command, List<Command> commands)
        => commands.Any(other => MappingRules.NameComparer.Equals(other.Name, command.Name)
            || (command.Trigger.IsBound && other.Trigger == command.Trigger));

    private static Command Rebound(Command command, IReadOnlyDictionary<GestureId, GestureId> idMap)
        => command.Trigger is Trigger.GestureTrigger gesture && idMap.TryGetValue(gesture.GestureId, out var final) && final != gesture.GestureId
            ? command with { Trigger = Trigger.ForGesture(final) }
            : command;
}
