using Augram.App.Components.CommandTree;
using Augram.App.Components.SearchBox;
using Augram.App.Components.StepList;
using Augram.App.ViewModels.Commands.Projection;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// How each Commands sub-tab cuts the mapping into sections (Joel, 2026-10-07): the Apps tab has one
/// section per app group, Global left out, and tags the rows of a group that has categories (Photoshop)
/// with the category's name; each of a group's hold remaps (F9, plan 0002) follows its group's section as a section nested
/// in it, holding the commands under that hold remap; the Global tab has the Global group's categories by name, every Global
/// command being in one (Uncategorized is an ordinary category the rules make for a command left without one, Joel
/// 2026-10-11). The one place that decides which commands a tab holds, which section a command
/// sits in, what a section header can do and what the header's Category dropdown offers; and, after the platform filter, what
/// the search box leaves (<see cref="Narrow"/>, plan 0009 decision 4).
/// </summary>
internal static class CommandSections
{
    /// <summary>What a hold remap's header says after its name: "Space · hold remap · 4 commands".</summary>
    public const string HoldRemapKind = "hold remap";

    /// <summary>The Global tab holds the Global group; the Apps tab every other group.</summary>
    public static bool Includes(CommandsScope scope, AppGroup group) => group.IsGlobal == (scope == CommandsScope.Global);

    /// <summary>
    /// The sections of a tab as they read on <paramref name="here"/>. An app group, a category or a command not used here (F8
    /// "Use on"; a command is not used here when its group, its category or itself leaves this platform out,
    /// <see cref="AppGroup.IsCommandUsedOn"/>) is left out unless <paramref name="showOtherPlatforms"/>, and is then marked
    /// "Windows only" and greyed; a category's commands follow it. A <paramref name="search"/> then narrows what is left
    /// (<see cref="Narrow"/>).
    /// </summary>
    public static IReadOnlyList<SectionItem> For(CommandsScope scope, MappingDocument document, IReadOnlySet<SectionId> expanded, Func<GestureId, Gesture?> findGesture, HostPlatform here, bool showOtherPlatforms = false, MouseButton? strokeButton = null, SearchQuery? search = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<SectionItem> sections = scope == CommandsScope.Global
            ? GlobalSections(document, expanded, findGesture, here, showOtherPlatforms)
            : [.. document.Groups.Where(group => !group.IsGlobal && (showOtherPlatforms || group.IsUsedOn(here))).SelectMany(group => GroupSections(document, group, expanded, findGesture, here, showOtherPlatforms))];
        if (strokeButton is { } stroke)
        {
            sections = [.. sections.Select(section => section with { Commands = [.. section.Commands.Select(item => WithStrokeButtonNote(item, stroke))] })];
        }

        return Narrow(sections, search ?? SearchQuery.Empty);
    }

    /// <summary>
    /// The tree rule of the search (plan 0009 decision 4): a section whose name matches shows with all its commands, expanded; a
    /// command that matches by name, or by its category tag on the Apps tab (the category is the second level there), shows
    /// under its section with the section's other commands hidden, the section expanded; everything else is hidden. A nested
    /// section (a hold remap) that shows keeps the section it is nested in shown and expanded, since the tree lists it only
    /// under an open parent. An empty query leaves the sections as they are, the saved expanded set in charge.
    /// </summary>
    public static IReadOnlyList<SectionItem> Narrow(IReadOnlyList<SectionItem> sections, SearchQuery search)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(search);
        if (search.IsEmpty)
        {
            return sections;
        }

        var shown = new Dictionary<SectionId, SectionItem>();
        foreach (var section in sections)
        {
            if (search.Matches(section.Name))
            {
                shown[section.Id] = section with { IsExpanded = true };
                continue;
            }

            var commands = section.Commands.Where(command => search.Matches(command.Name) || (command.CategoryLabel is { } category && search.Matches(category))).ToList();
            if (commands.Count > 0)
            {
                shown[section.Id] = section with { IsExpanded = true, Commands = commands };
            }
        }

        // A hold remap that shows needs its group's header open above it; the group's own commands stay hidden.
        foreach (var section in sections)
        {
            if (section.Id.Parent is { } parent && shown.ContainsKey(section.Id) && !shown.ContainsKey(parent) && sections.FirstOrDefault(item => item.Id == parent) is { } group)
            {
                shown[parent] = group with { IsExpanded = true, Commands = [] };
            }
        }

        return [.. sections.Where(section => shown.ContainsKey(section.Id)).Select(section => shown[section.Id])];
    }

    /// <summary>
    /// A trigger naming this machine's stroke button means the stroke button here (<c>HeldButtonsExtensions.ForStrokeButton</c>);
    /// the header says so in the anchor warning's place, since nothing is held back for it. Also said over a draft in the header.
    /// </summary>
    public static CommandItem WithStrokeButtonNote(CommandItem item, MouseButton strokeButton)
        => item.Trigger.IsBound && item.Trigger.Hold.Physical.Has(strokeButton)
            ? item with { AnchorWarning = $"{strokeButton} is the stroke button on this machine, so here it means the stroke button." }
            : item;

    /// <summary>
    /// The row's section: on the Apps tab its group or hold remap; on the Global tab its category (a stored Global command
    /// always has one; without one, as a Paste target before the store places it, the Global group itself).
    /// </summary>
    public static SectionId SectionOf(CommandsScope scope, AppGroup group, Command command)
        => scope == CommandsScope.Apps ? (group.HoldRemapOf(command) is { } holdRemap ? SectionId.ForHoldRemap(group.Id, holdRemap.Id) : SectionId.ForGroup(group.Id))
            : command.CategoryId is { } id ? SectionId.ForCategory(group.Id, id)
            : SectionId.ForGroup(group.Id);

    /// <summary>
    /// Global: its categories by name, no empty choice (every Global command is in one). An app group with categories: "No
    /// category", then its categories by name; none for an app group without categories (the dropdown hides).
    /// </summary>
    public static IReadOnlyList<CategoryChoice> Choices(AppGroup group)
    {
        var categories = Sorted(group).Select(category => new CategoryChoice(category.Id, category.Name));
        return group.IsGlobal ? [.. categories]
            : group.Categories.Count > 0 ? [CategoryChoice.None, .. categories]
            : [];
    }

    /// <summary>The group's section (its ordinary commands), then one nested section per hold remap shown here, by name.</summary>
    private static IEnumerable<SectionItem> GroupSections(MappingDocument document, AppGroup group, IReadOnlySet<SectionId> expanded, Func<GestureId, Gesture?> findGesture, HostPlatform here, bool showOtherPlatforms)
    {
        var id = SectionId.ForGroup(group.Id);
        var choices = Choices(group);
        var commands = group.OrdinaryCommandsOn(showOtherPlatforms ? null : here)
            .Select(command => Item(group, command, findGesture, here) with
            {
                Section = id,
                CategoryLabel = command.CategoryId is { } category ? group.FindCategory(category)?.Name : null,
                Categories = choices,
                NotInText = NotInText(document, command),
                AlsoInText = AlsoInText(document, command),
            })
            .ToList();
        var holdRemaps = group.HoldRemaps.Where(holdRemap => showOtherPlatforms || holdRemap.IsUsedOn(here)).ToList();
        yield return new SectionItem(id, group.Name, group.IsActive, expanded.Contains(id), commands)
        {
            CanEditDefinition = true,
            CanToggleActive = true,
            CanAddHoldRemap = true,
            CanExport = true,
            HoldRemapCount = holdRemaps.Count,
            IsElsewhere = !group.IsUsedOn(here),
            Note = PlatformNote(group, here),
        };

        foreach (var holdRemap in holdRemaps)
        {
            yield return HoldRemapSection(group, holdRemap, expanded, findGesture, here, showOtherPlatforms);
        }
    }

    /// <summary>
    /// A hold remap as a section nested in its group's ("Space · hold remap · 4 commands"): its commands (no category, no
    /// Category dropdown), its active box, rename, delete and copy; greyed "Windows only" when it or its group is not used here.
    /// </summary>
    private static SectionItem HoldRemapSection(AppGroup group, HoldRemap holdRemap, IReadOnlySet<SectionId> expanded, Func<GestureId, Gesture?> findGesture, HostPlatform here, bool showOtherPlatforms)
    {
        var id = SectionId.ForHoldRemap(group.Id, holdRemap.Id);
        var commands = group.HoldRemapCommandsOn(holdRemap, showOtherPlatforms ? null : here)
            .Select(command => Item(group, command, findGesture, here) with { Section = id })
            .ToList();
        return new SectionItem(id, holdRemap.Name, holdRemap.IsActive, expanded.Contains(id), commands)
        {
            KindText = HoldRemapKind,
            CanToggleActive = true,
            CanCopy = true,
            IsElsewhere = !group.IsUsedOn(here) || !holdRemap.IsUsedOn(here),
            Note = holdRemap.IsUsedOn(here) ? null : StepPlatformMarker.Only(holdRemap.UseOn),
        };
    }

    /// <summary>"Windows only" for a group not used here; "no macOS name" for one used here that has names, but neither its own nor a guess for this platform; else none.</summary>
    private static string? PlatformNote(AppGroup group, HostPlatform here)
    {
        if (!group.IsUsedOn(here))
        {
            return StepPlatformMarker.Only(group.UseOn);
        }

        return group.Matcher?.Summary(here).LacksNameHere == true ? $"no {here.DisplayName()} name" : null;
    }

    /// <summary>
    /// What the header's Not in row says (plan 0004): the Exclusions › Per command entries the command is not used over, by name
    /// ("Eyeris, Spine"), or <see cref="CommandItem.NoneNotIn"/>.
    /// </summary>
    public static string NotInText(MappingDocument document, Command command)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(command);
        var names = command.NotIn
            .Select(id => document.Ignored.FirstOrDefault(app => app.Id == id && app.IsPerCommand)?.Name)
            .OfType<string>()
            .Order(MappingRules.NameComparer)
            .ToList();
        return names.Count == 0 ? CommandItem.NoneNotIn : string.Join(", ", names);
    }

    /// <summary>
    /// What the header's Also in row says (plan 0005 decision 7): the Exclusions › Global entries the command still works over,
    /// by name ("Blender"), or <see cref="CommandItem.NoneNotIn"/>.
    /// </summary>
    public static string AlsoInText(MappingDocument document, Command command)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(command);
        var names = command.AlsoIn
            .Select(id => document.Ignored.FirstOrDefault(app => app.Id == id && app.IsGlobal)?.Name)
            .OfType<string>()
            .Order(MappingRules.NameComparer)
            .ToList();
        return names.Count == 0 ? CommandItem.NoneNotIn : string.Join(", ", names);
    }

    /// <summary>
    /// Each category by name, Uncategorized among them like any other (Joel, 2026-10-11); a category not used here
    /// (Joel, 2026-10-08) is left out unless <paramref name="showOtherPlatforms"/>, then greyed with "Windows only", and its
    /// commands go with it: hidden, or greyed. Each command carries what its Not in and Also in rows say.
    /// </summary>
    private static List<SectionItem> GlobalSections(MappingDocument document, IReadOnlySet<SectionId> expanded, Func<GestureId, Gesture?> findGesture, HostPlatform here, bool showOtherPlatforms)
    {
        var global = document.Global;
        var choices = Choices(global);
        var items = global.OrdinaryCommandsOn(showOtherPlatforms ? null : here)
            .Select(command => Item(global, command, findGesture, here) with
            {
                Section = SectionOf(CommandsScope.Global, global, command),
                Categories = choices,
                NotInText = NotInText(document, command),
                AlsoInText = AlsoInText(document, command),
            })
            .ToList();
        var sections = new List<SectionItem>();
        foreach (var category in Sorted(global))
        {
            var usedHere = category.IsUsedOn(here);
            if (!usedHere && !showOtherPlatforms)
            {
                continue;
            }

            var id = SectionId.ForCategory(global.Id, category.Id);
            sections.Add(new SectionItem(id, category.Name, global.IsActive, expanded.Contains(id), [.. items.Where(item => item.Section == id)])
            {
                CanExport = true,
                IsElsewhere = !usedHere,
                Note = usedHere ? null : StepPlatformMarker.Only(category.UseOn),
            });
        }

        return sections;
    }

    private static IEnumerable<CommandCategory> Sorted(AppGroup group) => group.Categories.OrderBy(category => category.Name, MappingRules.NameComparer);

    private static CommandItem Item(AppGroup group, Command command, Func<GestureId, Gesture?> findGesture, HostPlatform here)
        => CommandItemProjection.From(group, command, command.TriggerFor(here) is Trigger.GestureTrigger gesture ? findGesture(gesture.GestureId) : null, here);
}
