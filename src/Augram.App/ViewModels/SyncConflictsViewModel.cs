using Augram.App.Components.SyncConflictList;
using Augram.App.Declarations;
using Augram.App.Sync;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Sync;
using Augram.Core.Text;

namespace Augram.App.ViewModels;

/// <summary>
/// The conflict dialog (F8 sync, conflicts): one <see cref="SyncConflictEntry"/> per pending <see cref="SyncConflict"/>
/// (its name, kind and the other machine; both versions in words by <see cref="SyncConflictSideText"/>, a gesture as
/// its glyph, anything else as a one-line summary or "deleted") and the user's choice per conflict, Keep mine by
/// default. Keep both is offered only for a gesture or a command that exists on both machines (<see cref="Allowed"/>);
/// elsewhere Core would apply the nearest choice anyway. <see cref="Resolutions"/> is what Apply hands to <see cref="SyncService.Resolve"/>. Names in the summaries
/// (a command's group and gesture) are read from this machine's stores, as snapshots, when the dialog opens. The Augram file
/// import (plan 0003) reuses it for its differing items, with its own <see cref="Help"/> and the file as the other side.
/// </summary>
public sealed class SyncConflictsViewModel
{
    public const string KeepMineLabel = "Keep mine";
    public const string TakeTheirsLabel = "Take theirs";
    public const string KeepBothLabel = "Keep both";
    public const string SyncHelp =
        "Each item changed here and on another machine since they last synced. Keep mine sends this machine's version to the other one on the next sync; Take theirs uses the other machine's here; Keep both keeps yours and adds theirs beside it. Cancel leaves them pending; nothing blocks meanwhile.";

    private readonly SyncConflict[] _conflicts;
    private readonly SyncChoice[] _choices;
    private readonly SyncConflictSideText _sides;

    /// <summary><c>help</c> is the dialog's instructions (its ⓘ): the sync's (<see cref="SyncHelp"/>) unless the host has its own.</summary>
    public SyncConflictsViewModel(IReadOnlyList<SyncConflict> conflicts, IReadOnlyList<Gesture> gestures, MappingDocument mapping, StepRegistry? steps = null, string? help = null)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(mapping);
        _conflicts = [.. conflicts];
        _choices = [.. conflicts.Select(_ => SyncChoice.KeepMine)];
        _sides = new SyncConflictSideText(gestures, mapping, steps ?? StepRegistry.BuiltIn);
        Help = help ?? SyncHelp;
        Entries = [.. _conflicts.Select(Entry)];
    }

    /// <summary>One per conflict, each showing its current choice; rebuilt by <see cref="ChooseAll"/>.</summary>
    public IReadOnlyList<SyncConflictEntry> Entries { get; private set; }

    /// <summary>The dialog's instructions, shown in its ⓘ.</summary>
    public string Help { get; }

    /// <summary>The choice per conflict, in <see cref="Entries"/> order.</summary>
    public IReadOnlyList<SyncChoice> Choices => _choices;

    /// <summary>"2 conflicts with Mac", for the dialog's title line.</summary>
    public string Summary => Describe(_conflicts);

    /// <summary>Keep mine and Take theirs always; Keep both for a gesture or a command present on both machines: Core's rule, <see cref="SyncChoiceRules.Allowed"/>.</summary>
    public static IReadOnlyList<SyncChoice> Allowed(SyncConflict conflict) => SyncChoiceRules.Allowed(conflict);

    /// <summary>"1 conflict with Mac", "3 conflicts with Mac and PC-WORK"; empty when there are none.</summary>
    public static string Describe(IReadOnlyList<SyncConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        if (conflicts.Count == 0)
        {
            return string.Empty;
        }

        var machines = conflicts.Select(conflict => conflict.MachineName).Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var count = Sentence.Count(conflicts.Count, "conflict");
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

    /// <summary>
    /// "Apply to all": every conflict that offers <paramref name="choice"/> takes it; one that does not (Keep both on an app
    /// group) keeps its own. <see cref="Entries"/> is rebuilt so a list shows the new choices; false when nothing changed.
    /// </summary>
    public bool ChooseAll(SyncChoice choice)
    {
        var changed = false;
        for (var i = 0; i < _conflicts.Length; i++)
        {
            if (_choices[i] != choice && Allowed(_conflicts[i]).Contains(choice))
            {
                _choices[i] = choice;
                changed = true;
            }
        }

        if (changed)
        {
            Entries = [.. _conflicts.Select(Entry)];
        }

        return changed;
    }

    /// <summary>Every conflict with its choice, in order: what Apply resolves.</summary>
    public IReadOnlyList<SyncResolution> Resolutions() => [.. _conflicts.Select((conflict, i) => new SyncResolution(conflict, _choices[i]))];

    private SyncConflictEntry Entry(SyncConflict conflict, int index)
    {
        var detail = conflict.MachineName.Length > 0 ? $"{SyncItemNouns.Heading(conflict.Kind)} · with {conflict.MachineName}" : SyncItemNouns.Heading(conflict.Kind);
        var choices = Allowed(conflict).Select(choice => new Choice<SyncChoice>(Label(choice), choice)).ToList();
        return new SyncConflictEntry(conflict.Name, detail, _sides.Side(conflict, conflict.LocalContent), _sides.Side(conflict, conflict.RemoteContent), choices, _choices[index]);
    }

    public static string Label(SyncChoice choice) => choice switch
    {
        SyncChoice.KeepMine => KeepMineLabel,
        SyncChoice.TakeTheirs => TakeTheirsLabel,
        _ => KeepBothLabel,
    };
}
