using Augram.App.Dialogs;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// Shows the pending conflicts in a <see cref="SyncConflictWindow"/> over a <see cref="SyncConflictsViewModel"/> built
/// against this machine's stores, modal to the main window (Resolve… is on the Options page, so it is open; alone and not
/// activated otherwise, <see cref="DialogHost.ShowModalAsync"/>). Apply returns every conflict with its choice; Cancel
/// returns null. Nothing here resolves anything.
/// </summary>
public sealed class SyncConflictPresenter : ISyncConflictPresenter
{
    private readonly GestureLibrary _gestures;
    private readonly MappingStore _mapping;

    public SyncConflictPresenter(GestureLibrary gestures, MappingStore mapping)
    {
        _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    public async Task<IReadOnlyList<SyncResolution>?> ResolveAsync(IReadOnlyList<SyncConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        if (conflicts.Count == 0)
        {
            return null;
        }

        var viewModel = new SyncConflictsViewModel(conflicts, _gestures.All, _mapping.Current);
        var window = new SyncConflictWindow(viewModel);
        await DialogHost.ShowModalAsync(window, activateWhenAlone: false).ConfigureAwait(true);
        return window.Applied ? viewModel.Resolutions() : null;
    }
}
