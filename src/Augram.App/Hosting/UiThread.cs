using Avalonia.Threading;

namespace Augram.App.Hosting;

/// <summary>
/// The one way App code reaches the Avalonia UI thread (ADR-0002 §5a). <see cref="Post"/> is the marshal the composition root
/// hands every module (<c>EngineModuleOptions.Marshal</c>, <c>SyncModuleOptions.Marshal</c>) and the default of every class that
/// takes an <c>Action&lt;Action&gt;</c> marshal; <see cref="RunOrPost"/> is what a store listener uses, so a change made on the UI
/// thread (every edit, and the headless tests) projects at once and one from a worker is posted. Tests that must not touch the
/// dispatcher pass their own marshal (<c>action =&gt; action()</c>) instead.
/// </summary>
public static class UiThread
{
    /// <summary>True on the UI thread.</summary>
    public static bool IsCurrent => Dispatcher.UIThread.CheckAccess();

    /// <summary>Queues <paramref name="action"/> on the UI thread at normal priority, from any thread, and returns at once.</summary>
    public static void Post(Action action) => Dispatcher.UIThread.Post(action);

    /// <summary>Runs <paramref name="action"/> now when called on the UI thread, else posts it (<see cref="Post"/>).</summary>
    public static void RunOrPost(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsCurrent)
        {
            action();
        }
        else
        {
            Post(action);
        }
    }

    /// <summary>Runs <paramref name="function"/> on the UI thread at normal priority and waits for it; <paramref name="cancel"/> gives up waiting (<see cref="OperationCanceledException"/>).</summary>
    public static T Invoke<T>(Func<T> function, CancellationToken cancel) => Dispatcher.UIThread.Invoke(function, DispatcherPriority.Normal, cancel);
}
