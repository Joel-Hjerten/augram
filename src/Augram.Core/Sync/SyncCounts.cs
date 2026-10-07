namespace Augram.Core.Sync;

/// <summary>What a sync did to this machine's gestures and mapping, per item kind (added, changed, deleted).</summary>
public sealed record SyncCounts(
    SyncKindCounts Gestures,
    SyncKindCounts Groups,
    SyncKindCounts Categories,
    SyncKindCounts Commands,
    SyncKindCounts Ignored)
{
    public static SyncCounts None { get; } = new(SyncKindCounts.None, SyncKindCounts.None, SyncKindCounts.None, SyncKindCounts.None, SyncKindCounts.None);

    public int Total => Gestures.Total + Groups.Total + Categories.Total + Commands.Total + Ignored.Total;

    public bool IsEmpty => Total == 0;

    /// <summary>True when anything but gestures changed: the mapping store needs the result.</summary>
    public bool MappingChanged => Groups.Total + Categories.Total + Commands.Total + Ignored.Total > 0;

    public SyncKindCounts For(SyncItemKind kind) => kind switch
    {
        SyncItemKind.Gesture => Gestures,
        SyncItemKind.Group => Groups,
        SyncItemKind.Category => Categories,
        SyncItemKind.Command or SyncItemKind.CommandVersion => Commands,
        _ => Ignored,
    };

    /// <summary>Items added, changed (content differs) and deleted going from <paramref name="before"/> to <paramref name="after"/>.</summary>
    public static SyncCounts Between(SyncItemSet before, SyncItemSet after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        // A command's own version counts as a change of the command: one line in the log, not a kind of its own.
        static SyncItemKind Counted(SyncItem item) => item.Kind == SyncItemKind.CommandVersion ? SyncItemKind.Command : item.Kind;
        var counts = Enum.GetValues<SyncItemKind>().ToDictionary(kind => kind, _ => SyncKindCounts.None);
        foreach (var item in after)
        {
            var old = before.Find(item.Key);
            if (old is null)
            {
                counts[Counted(item)] = counts[Counted(item)].Plus(new(1, 0, 0));
            }
            else if (!string.Equals(old.Content, item.Content, StringComparison.Ordinal))
            {
                counts[Counted(item)] = counts[Counted(item)].Plus(new(0, 1, 0));
            }
        }

        foreach (var item in before.Where(item => !after.Contains(item.Key)))
        {
            counts[Counted(item)] = counts[Counted(item)].Plus(new(0, 0, 1));
        }

        return new(
            counts[SyncItemKind.Gesture],
            counts[SyncItemKind.Group],
            counts[SyncItemKind.Category],
            counts[SyncItemKind.Command],
            counts[SyncItemKind.Ignored]);
    }

    /// <summary>"gestures +1 ~0 -0, commands +0 ~2 -1": the kinds that moved, for the log.</summary>
    public override string ToString()
    {
        var parts = Enum.GetValues<SyncItemKind>()
            .Where(kind => kind != SyncItemKind.CommandVersion && For(kind).Total > 0)
            .Select(kind => $"{Label(kind)} {For(kind)}")
            .ToArray();
        return parts.Length == 0 ? "no changes" : string.Join(", ", parts);
    }

    private static string Label(SyncItemKind kind) => kind switch
    {
        SyncItemKind.Gesture => "gestures",
        SyncItemKind.Group => "groups",
        SyncItemKind.Category => "categories",
        SyncItemKind.Command => "commands",
        _ => "ignored apps",
    };
}
