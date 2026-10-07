using Augram.App.Hosting;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.Sync;

/// <summary>
/// Shows the pending conflicts in a <see cref="SyncConflictWindow"/> over a <see cref="SyncConflictsViewModel"/> built
/// against this machine's stores, modal to the main window (Resolve… is on the Options page, so it is open). Apply
/// returns every conflict with its choice; Cancel returns null. Nothing here resolves anything.
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
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            window.Show();
            await closed.Task.ConfigureAwait(true);
        }

        return window.Applied ? viewModel.Resolutions() : null;
    }
}
