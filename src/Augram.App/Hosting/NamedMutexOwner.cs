namespace Augram.App.Hosting;

/// <summary>
/// Holds a named <see cref="Mutex"/> on a thread of its own. A mutex belongs to the thread that acquired it and only that
/// thread can release it, while a take-over waits up to 10 s for it without blocking the UI thread and a pool thread may
/// never run again to release it. So one parked thread acquires (waiting up to the given time), holds it until
/// <see cref="Dispose"/>, then releases it. A mutex abandoned by a process that ended without releasing it (a crash, a
/// kill) counts as acquired. Two owners in one process behave like two processes, which the tests rely on.
/// </summary>
internal sealed class NamedMutexOwner : IDisposable
{
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(2);
    private readonly ManualResetEventSlim _release = new();
    private readonly TaskCompletionSource<bool> _acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private int _disposed;

    private NamedMutexOwner(string name, NamedWaitHandleOptions options, TimeSpan wait)
    {
        _thread = new Thread(() => Own(name, options, wait), maxStackSize: 256 * 1024) { IsBackground = true, Name = "augram-instance-mutex" };
        _thread.Start();
    }

    /// <summary>The owner once the mutex is held; null when another holder kept it for all of <paramref name="wait"/>.</summary>
    public static async Task<NamedMutexOwner?> AcquireAsync(string name, NamedWaitHandleOptions options, TimeSpan wait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var owner = new NamedMutexOwner(name, options, wait);
        bool acquired;
        try
        {
            acquired = await owner._acquired.Task.ConfigureAwait(false);
        }
        catch
        {
            owner.Dispose();
            throw;
        }

        if (acquired)
        {
            return owner;
        }

        owner.Dispose();
        return null;
    }

    /// <summary>Releases the mutex and waits briefly for its thread, so the name is free when this returns.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _release.Set();
        if (Thread.CurrentThread != _thread && _thread.Join(ReleaseTimeout))
        {
            _release.Dispose();
        }
    }

    private void Own(string name, NamedWaitHandleOptions options, TimeSpan wait)
    {
        Mutex mutex;
        try
        {
            mutex = new Mutex(initiallyOwned: false, name, options, out _);
        }
        catch (Exception e)
        {
            // Another kind of object owns the name, or the OS refused it: the caller sees the exception, as before the owner thread existed.
            _acquired.TrySetException(e);
            return;
        }

        using (mutex)
        {
            bool owned;
            try
            {
                owned = mutex.WaitOne(wait);
            }
            catch (AbandonedMutexException)
            {
                // The previous holder ended without releasing: the mutex is ours now.
                owned = true;
            }

            _acquired.TrySetResult(owned);
            if (!owned)
            {
                return;
            }

            _release.Wait();
            mutex.ReleaseMutex();
        }
    }
}
