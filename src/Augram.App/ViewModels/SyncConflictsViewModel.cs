using System.Globalization;
using Augram.App.Components.SyncConflictList;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Sync;

namespace Augram.App.ViewModels;

/// <summary>
/// The conflict dialog (F8 sync, conflicts): one <see cref="SyncConflictEntry"/> per pending <see cref="SyncConflict"/>
/// (its name, kind and the other machine; both versions, a gesture as its glyph, anything else as a one-line summary
/// or "deleted") and the user's choice per conflict, Keep mine by default. Keep both is offered only for a gesture
/// or a command that exists on both machines (<see cref="Allowed"/>); elsewhere Core would apply the nearest choice
/// anyway. <see cref="Resolutions"/> is what Apply hands to <see cref="SyncService.Resolve"/>. Names in the summaries
/// (a command's group and gesture) are read from this machine's stores, as snapshots, when the dialog opens.
/// </summary>
public sealed class SyncConflictsViewModel
{
    public const string KeepMineLabel = "Keep mine";
    public const string TakeTheirsLabel = "Take theirs";
    public const string KeepBothLabel = "Keep both";
    public const string Help =
        "Each item changed here and on another machine since they last synced. Keep mine sends this machine's version to the other one on the next sync; Take theirs uses the other machine's here; Keep both keeps yours and adds theirs beside it. Cancel leaves them pending; nothing blocks meanwhile.";

    private readonly SyncConflict[] _conflicts;
    private readonly SyncChoice[] _choices;
    private readonly IReadOnlyList<Gesture> _gestures;
    private readonly MappingDocument _mapping;
    private readonly StepRegistry _steps;

    public SyncConflictsViewModel(IReadOnlyList<SyncConflict> conflicts, IReadOnlyList<Gesture> gestures, MappingDocument mapping, StepRegistry? steps = null)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(mapping);
        _conflicts = [.. conflicts];
        _choices = [.. conflicts.Select(_ => SyncChoice.KeepMine)];
        _gestures = gestures;
        _mapping = mapping;
        _steps = steps ?? StepRegistry.BuiltIn;
        Entries = [.. _conflicts.Select(Entry)];
    }

    public IReadOnlyList<SyncConflictEntry> Entries { get; }

    /// <summary>The choice per conflict, in <see cref="Entries"/> order.</summary>
    public IReadOnlyList<SyncChoice> Choices => _choices;

    /// <summary>"2 conflicts with Mac", for the dialog's title line.</summary>
    public string Summary => Describe(_conflicts);

    /// <summary>Keep mine and Take theirs always; Keep both for a gesture or a command present on both machines.</summary>
    public static IReadOnlyList<SyncChoice> Allowed(SyncConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        return conflict.Kind is SyncItemKind.Gesture or SyncItemKind.Command && !conflict.DeletedHere && !conflict.DeletedThere
            ? [SyncChoice.KeepMine, SyncChoice.TakeTheirs, SyncChoice.KeepBoth]
            : [SyncChoice.KeepMine, SyncChoice.TakeTheirs];
    }

    /// <summary>"1 conflict with Mac", "3 conflicts with Mac and PC-WORK"; empty when there are none.</summary>
    public static string Describe(IReadOnlyList<SyncConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        if (conflicts.Count == 0)
        {
            return string.Empty;
        }

        var machines = conflicts.Select(conflict => conflict.MachineName).Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var count = string.Create(CultureInfo.InvariantCulture, $"{conflicts.Count} conflict{(conflicts.Count == 1 ? string.Empty : "s")}");
        return machines.Count == 0 ? count : $"{count} with {string.Join(" and ", machines)}";
    }

    /// <summary>Sets the choice for the conflict at <paramref name="index"/>; false (and nothing changes) when that conflict does not offer it.</summary>
    public bool Choose(int index, SyncChoice choice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _conflicts.Length);
        if (!Allowed(_conflicts[index]).Contains(choice))
        {
            return false;
        }

        _choices[index] = choice;
        return true;
    }

    /// <summary>Every conflict with its choice, in order: what Apply resolves.</summary>
    public IReadOnlyList<SyncResolution> Resolutions() => [.. _conflicts.Select((conflict, i) => new SyncResolution(conflict, _choices[i]))];

    private SyncConflictEntry Entry(SyncConflict conflict, int index)
    {
        var detail = conflict.MachineName.Length > 0 ? $"{KindLabel(conflict.Kind)} · with {conflict.MachineName}" : KindLabel(conflict.Kind);
        var choices = Allowed(conflict).Select(choice => new Choice<SyncChoice>(Label(choice), choice)).ToList();
        return new SyncConflictEntry(conflict.Name, detail, Side(conflict, conflict.LocalContent), Side(conflict, conflict.RemoteContent), choices, _choices[index]);
    }

    public static string KindLabel(SyncItemKind kind) => kind switch
    {
        SyncItemKind.Gesture => "Gesture",
        SyncItemKind.Group => "App group",
        SyncItemKind.Category => "Category",
        SyncItemKind.Command => "Command",
        SyncItemKind.CommandVersion => "Own steps",
        _ => "Ignored app",
    };

    public static string Label(SyncChoice choice) => choice switch
    {
        SyncChoice.KeepMine => KeepMineLabel,
        SyncChoice.TakeTheirs => TakeTheirsLabel,
        _ => KeepBothLabel,
    };

    private SyncConflictSide Side(SyncConflict conflict, string? content)
    {
        if (content is null)
        {
            return SyncConflictSide.Deleted;
        }

        SyncItem item;
        try
        {
            item = SyncItem.Parse(conflict.Key, content, _steps);
        }
        catch (ConfigFormatException)
        {
            return new SyncConflictSide("(this version cannot be read here)");
        }

        return item switch
        {
            SyncItem.GestureItem gesture => new SyncConflictSide(GestureText(gesture.Gesture), gesture.Gesture.Samples.Count > 0 ? gesture.Gesture.Samples[0] : null),
            SyncItem.CommandItem command => new SyncConflictSide(CommandText(command.GroupId, command.Command)),
            SyncItem.GroupItem group => new SyncConflictSide(GroupText(group.Header)),
            SyncItem.IgnoredItem ignored => new SyncConflictSide(IgnoredText(ignored.App)),
            SyncItem.VersionItem version => new SyncConflictSide(VersionText(version.Version)),
            _ => new SyncConflictSide(item.Name),
        };
    }

    private static string GestureText(Gesture gesture)
    {
        var samples = string.Create(CultureInfo.InvariantCulture, $"{gesture.Samples.Count} sample{(gesture.Samples.Count == 1 ? string.Empty : "s")}");
        return $"{gesture.Name} · {samples}{(gesture.IsActive ? string.Empty : " · inactive")}";
    }

    private string CommandText(GroupId groupId, Command command)
    {
        var group = _mapping.Groups.FirstOrDefault(candidate => candidate.Id == groupId)?.Name ?? "another group";
        var trigger = command.Trigger switch
        {
            Trigger.GestureTrigger gesture => _gestures.FirstOrDefault(candidate => candidate.Id == gesture.GestureId) is { } known
                ? $"gesture {known.Name}"
                : "a gesture not on this machine",
            _ => command.Trigger.Describe(),
        };
        var steps = command.Steps.Count == 0 ? "no steps" : string.Join(", ", command.Steps.Select(step => step.Step.Summary));
        return $"{command.Name} in {group}: {trigger} → {steps}{(command.IsActive ? string.Empty : " (inactive)")}";
    }

    private static string VersionText(CommandVersion version)
    {
        var platform = version.Platform == HostPlatform.MacOS ? "macOS" : "Windows";
        var steps = version.Steps.Count == 0 ? "no steps (does nothing there)" : string.Join(", ", version.Steps.Select(step => step.Step.SummaryOn(step.AuthoredOn)));
        return $"{platform} steps: {steps}";
    }

    private static string GroupText(AppGroup group)
    {
        var matches = group.Matcher is { WindowsProcessNames.Count: > 0 } matcher ? "matches " + string.Join(", ", matcher.WindowsProcessNames) : "matches no app yet";
        return $"{group.Name}: {matches}{(group.SuppressGlobals ? ", suppresses global commands" : string.Empty)}{(group.IsActive ? string.Empty : " (inactive)")}";
    }

    private static string IgnoredText(IgnoredApp app)
    {
        var matches = app.Matcher.WindowsProcessNames.Count > 0 ? string.Join(", ", app.Matcher.WindowsProcessNames) : "no app yet";
        return $"{app.Name}: {matches}{(app.DisableEntirely ? ", turns Augram off" : string.Empty)}{(app.IsActive ? string.Empty : " (inactive)")}";
    }
}
