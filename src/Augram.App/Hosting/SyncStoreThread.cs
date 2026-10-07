using Augram.Core.Sync;
using Avalonia.Threading;

namespace Augram.App.Hosting;

/// <summary>
/// The <c>onStoreThread</c> delegate <see cref="SyncCoordinator"/> applies through (Core/Sync README: threading):
/// runs the function on the stores' thread, the Avalonia UI thread in the app, synchronously, and returns its
/// result; called on the UI thread it runs at once. While the function runs, <see cref="IsApplying"/> is true, so
/// <see cref="SyncService"/> can tell the store changes a sync makes from the user's own (it reads the flag in the
/// stores' <c>Changed</c> handlers, which run inside the function on the same thread). After <see cref="Stop"/> a
/// waiting call returns <see cref="SyncApplied.Failed"/> at once and nothing more is applied, so shutdown never
/// waits on a UI thread that is no longer pumping and a sync never lands after the app closed.
/// </summary>
public sealed class SyncStoreThread
{
    public const string ClosingError = "Augram is closing; the sync was not applied.";

    private readonly Func<Func<SyncApplied>, CancellationToken, SyncApplied> _marshal;
    private readonly CancellationTokenSource _stopping = new();
    private volatile bool _stopped;
    private volatile bool _applying;

    /// <param name="marshal">Runs the function on the store thread; null for the Avalonia UI thread. Tests pass <c>(f, _) =&gt; f()</c>.</param>
    public SyncStoreThread(Func<Func<SyncApplied>, CancellationToken, SyncApplied>? marshal = null)
    {
        _marshal = marshal ?? OnUiThread;
    }

    /// <summary>True while a sync is changing the stores; read it on the store thread, inside a store's <c>Changed</c> handler.</summary>
    public bool IsApplying => _applying;

    /// <summary>The delegate handed to <see cref="SyncCoordinator"/>.</summary>
    public SyncApplied Run(Func<SyncApplied> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (_stopped)
        {
            return SyncApplied.Failed(ClosingError);
        }

        return _marshal(() =>
        {
            // Checked again on the store thread: Stop runs there too, so a call queued before it never applies after it.
            if (_stopped)
            {
                return SyncApplied.Failed(ClosingError);
            }

            _applying = true;
            try
            {
                return apply();
            }
            finally
            {
                _applying = false;
            }
        }, _stopping.Token);
    }

    /// <summary>No sync applies from now on; a call waiting for the UI thread gives up.</summary>
    public void Stop()
    {
        _stopped = true;
        _stopping.Cancel();
    }

    private static SyncApplied OnUiThread(Func<SyncApplied> apply, CancellationToken stopping)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return apply();
        }

        try
        {
            return Dispatcher.UIThread.Invoke(apply, DispatcherPriority.Normal, stopping);
        }
        catch (OperationCanceledException)
        {
            return SyncApplied.Failed(ClosingError);
        }
    }
}
