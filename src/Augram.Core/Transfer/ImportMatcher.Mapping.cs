using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.Core.Transfer;

/// <summary>
/// The mapping half of <see cref="ImportMatcher"/>: groups (Global always to Global, which has the same id everywhere),
/// then within each matched group its categories, hold remaps and commands, then the ignored apps. Command ids are unique
/// across the whole mapping, so a command matched by id may sit in another group here (a move, which the merge reads as a
/// change); one matched by name is looked for among its siblings in the matched group only (its hold remap's commands, or
/// the group's ordinary ones: <see cref="CommandNames"/>). A file group with no counterpart here keeps its ids.
/// </summary>
internal sealed partial class ImportMatcher
{
    private HashSet<CommandId> _localCommands = [];
    private HashSet<CommandId> _claimedCommands = [];

    private MappingDocument Mapping(MappingDocument file, MappingDocument local)
    {
        var localGroups = local.Groups.ToDictionary(group => group.Id);
        var groupIds = Pair(
            file.Groups,
            local.Groups,
            group => group.Id,
            group => group.Name,
            SyncItemKey.ForGroup,
            (ImportMatchKind.Name, (group, twin) => !twin.IsGlobal && MappingRules.NameComparer.Equals(group.Name, twin.Name)));
        _localCommands = local.AllCommands().Select(pair => pair.Command.Id).ToHashSet();
        _claimedCommands = file.AllCommands().Select(pair => pair.Command.Id).Where(_localCommands.Contains).ToHashSet();
        var groups = file.Groups
            .Select(group => group with { Id = groupIds.GetValueOrDefault(group.Id, group.Id) })
            .Select(group => Group(group, localGroups.GetValueOrDefault(group.Id)))
            .ToArray();
        return new MappingDocument(groups, Ignored(file.Ignored, local.Ignored));
    }

    /// <summary>The file group (already under its local id) with its children lined up against <paramref name="local"/>, the group here; null for a new group.</summary>
    private AppGroup Group(AppGroup group, AppGroup? local)
    {
        if (local is null)
        {
            return group with { Commands = [.. group.Commands.Select(command => command.WithGesturesReplaced(_gestureIds))] };
        }

        var categoryIds = Pair(
            group.Categories,
            local.Categories,
            category => category.Id,
            category => category.Name,
            id => SyncItemKey.ForCategory(local.Id, id),
            (ImportMatchKind.Name, (category, twin) => MappingRules.NameComparer.Equals(category.Name, twin.Name)));
        var holdRemapIds = Pair(
            group.HoldRemaps,
            local.HoldRemaps,
            holdRemap => holdRemap.Id,
            holdRemap => holdRemap.Name,
            id => SyncItemKey.ForHoldRemap(local.Id, id),
            (ImportMatchKind.HoldKey, (holdRemap, twin) => holdRemap.HoldKey != KeyCode.None && holdRemap.HoldKey == twin.HoldKey),
            (ImportMatchKind.Name, (holdRemap, twin) => MappingRules.NameComparer.Equals(holdRemap.Name, twin.Name)));
        var commands = group.Commands
            .Select(command => command with
            {
                CategoryId = command.CategoryId is { } category ? categoryIds.GetValueOrDefault(category, category) : null,
                HoldRemapId = command.HoldRemapId is { } holdRemap ? holdRemapIds.GetValueOrDefault(holdRemap, holdRemap) : null,
            })
            .Select(command => Command(command, local).WithGesturesReplaced(_gestureIds))
            .ToArray();
        return group with
        {
            Categories = [.. group.Categories.Select(category => category with { Id = categoryIds.GetValueOrDefault(category.Id, category.Id) })],
            HoldRemaps = [.. group.HoldRemaps.Select(holdRemap => holdRemap with { Id = holdRemapIds.GetValueOrDefault(holdRemap.Id, holdRemap.Id) })],
            Commands = commands,
        };
    }

    /// <summary>The command under the id of its sibling here with the same name, when its own id is unknown here and that sibling is not taken.</summary>
    private Command Command(Command command, AppGroup local)
    {
        if (_localCommands.Contains(command.Id))
        {
            return command;
        }

        var twin = local.Commands.FirstOrDefault(candidate => !_claimedCommands.Contains(candidate.Id)
            && candidate.HoldRemapId == command.HoldRemapId
            && MappingRules.NameComparer.Equals(candidate.Name, command.Name));
        if (twin is null)
        {
            return command;
        }

        _claimedCommands.Add(twin.Id);
        _matches[SyncItemKey.ForCommand(twin.Id)] = new ImportMatch(ImportMatchKind.Name, command.Name);
        return command with { Id = twin.Id };
    }

    /// <summary>
    /// File id → local id for items whose ids are unknown among <paramref name="local"/>: each takes the first local item not
    /// yet claimed that the first of <paramref name="rules"/> able to pair it accepts (a hold key before a name), and the match
    /// is recorded under the local item's <paramref name="key"/>.
    /// </summary>
    private Dictionary<TId, TId> Pair<T, TId>(
        IReadOnlyList<T> file,
        IReadOnlyList<T> local,
        Func<T, TId> id,
        Func<T, string> name,
        Func<TId, SyncItemKey> key,
        params (ImportMatchKind By, Func<T, T, bool> Matches)[] rules)
        where TId : notnull
    {
        var localIds = local.Select(id).ToHashSet();
        var claimed = file.Select(id).Where(localIds.Contains).ToHashSet();
        var pairs = new Dictionary<TId, TId>();
        foreach (var item in file.Where(item => !localIds.Contains(id(item))))
        {
            foreach (var (by, matches) in rules)
            {
                var twin = local.FirstOrDefault(candidate => !claimed.Contains(id(candidate)) && matches(item, candidate));
                if (twin is not null)
                {
                    claimed.Add(id(twin));
                    pairs[id(item)] = id(twin);
                    _matches[key(id(twin))] = new ImportMatch(by, name(item));
                    break;
                }
            }
        }

        return pairs;
    }

    private IgnoredApp[] Ignored(IReadOnlyList<IgnoredApp> file, IReadOnlyList<IgnoredApp> local)
    {
        var ids = Pair(
            file,
            local,
            app => app.Id,
            app => app.Name,
            SyncItemKey.ForIgnored,
            (ImportMatchKind.Name, (app, twin) => MappingRules.NameComparer.Equals(app.Name, twin.Name)));
        return [.. file.Select(app => app with { Id = ids.GetValueOrDefault(app.Id, app.Id) })];
    }
}
