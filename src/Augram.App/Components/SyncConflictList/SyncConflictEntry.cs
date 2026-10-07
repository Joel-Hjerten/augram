using Augram.App.Declarations;
using Augram.Core.Sync;

namespace Augram.App.Components.SyncConflictList;

/// <summary>
/// One pending sync conflict as a <see cref="SyncConflictRow"/> shows it (F8 sync): the item's name, a detail line
/// (its kind and the other machine), this machine's and the other machine's version, the choices offered (Keep both
/// only where it applies) and the one selected. Presentational: the view model builds it, the row never decides.
/// </summary>
public sealed record SyncConflictEntry(
    string Name,
    string Detail,
    SyncConflictSide Mine,
    SyncConflictSide Theirs,
    IReadOnlyList<Choice<SyncChoice>> Choices,
    SyncChoice Selected);
