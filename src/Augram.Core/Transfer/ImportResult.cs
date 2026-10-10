using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.Core.Transfer;

/// <summary>
/// What an import leaves (plan 0003): the gestures and mapping after the choices, repaired and validated like a sync merge
/// (safe to hand to <c>ReplaceAll</c>), the options, what changed per kind (<see cref="SyncCounts"/>, relative to the
/// configuration the plan was made from; an import only adds and changes), the repairs and the notes (a choice applied as the
/// nearest one). <see cref="ApplyTo"/> commits it as one undo step per store that changed, like a sync apply.
/// </summary>
public sealed class ImportResult
{
    private readonly ConfigDocument _seen;

    internal ImportResult(
        IReadOnlyList<Gesture> gestures,
        MappingDocument mapping,
        Settings settings,
        SyncCounts counts,
        IReadOnlyList<SyncRepair> repairs,
        IReadOnlyList<string> notes,
        ConfigDocument seen)
    {
        Gestures = gestures;
        Mapping = mapping;
        Settings = settings;
        Counts = counts;
        Repairs = repairs;
        Notes = notes;
        _seen = seen;
    }

    public IReadOnlyList<Gesture> Gestures { get; }

    public MappingDocument Mapping { get; }

    /// <summary>The options afterwards: this machine's, or with the file's preferences when they were taken.</summary>
    public Settings Settings { get; }

    public bool SettingsChanged => Settings != _seen.Settings;

    public SyncCounts Counts { get; }

    /// <summary>What the result needed so it passes the rules: an arriving item renamed " (2)", a taken trigger unbound, and so on (the sync's <see cref="SyncRepairKind"/>s).</summary>
    public IReadOnlyList<SyncRepair> Repairs { get; }

    /// <summary>A Keep both applied as Take theirs where it does not apply, one line each.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>Nothing would change.</summary>
    public bool IsEmpty => Counts.IsEmpty && !SettingsChanged;

    /// <summary>
    /// Replaces what changed, one store call each (one undo step per store). Runs on the stores' thread. Refused (false,
    /// nothing applied) when a store no longer holds the snapshot the plan was made from (a sync applied while the review
    /// was open): plan again from the stores and resolve with the same choices.
    /// </summary>
    public bool ApplyTo(SettingsStore settings, GestureLibrary gestures, MappingStore mapping)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(mapping);
        if (!ReferenceEquals(gestures.All, _seen.Gestures) || !ReferenceEquals(mapping.Current, _seen.Mapping) || !ReferenceEquals(settings.Current, _seen.Settings))
        {
            return false;
        }

        if (Counts.Gestures.Total > 0)
        {
            gestures.ReplaceAll(Gestures);
        }

        if (Counts.MappingChanged)
        {
            mapping.ReplaceAll(Mapping);
        }

        if (SettingsChanged)
        {
            settings.Apply(_ => Settings);
        }

        return true;
    }
}
