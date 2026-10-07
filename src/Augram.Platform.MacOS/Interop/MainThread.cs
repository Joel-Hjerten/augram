using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// Runs work on the main thread and waits for it, for the AppKit calls the command executor needs (<c>NSScreen</c>, and
/// Accessibility calls on Augram's own windows, which AppKit answers in-process on the calling thread). The UI toolkit's
/// main loop drains the main dispatch queue and the UI thread never waits on the executor, so the wait is a frame or two.
/// A process with no main loop (a test host) never drains it: after <see cref="Timeout"/> the work is withdrawn, never
/// run late, and <see cref="TryInvoke"/> returns false. Runs inline when already on the main thread. An exception thrown
/// by the work is rethrown on the caller.
/// </summary>
[SupportedOSPlatform("macos")]
internal static unsafe class MainThread
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static readonly Lazy<nint> Queue = new(MacNative.MainQueue);

    private interface IJob
    {
        void Run();
    }

    public static bool TryInvoke<T>(Func<T> work, out T result)
    {
        if (MacNative.pthread_main_np() != 0)
        {
            result = work();
            return true;
        }

        var job = new Job<T>(work);
        MacNative.dispatch_async_f(Queue.Value, GCHandle.ToIntPtr(GCHandle.Alloc(job)), &Run);
        return job.Wait(out result);
    }

    [UnmanagedCallersOnly]
    private static void Run(nint context)
    {
        var handle = GCHandle.FromIntPtr(context);
        var job = (IJob)handle.Target!;
        handle.Free();
        job.Run();
    }

    private sealed class Job<T>(Func<T> work) : IJob
    {
        private const int Pending = 0;
        private const int Running = 1;
        private const int Withdrawn = 2;

        private readonly ManualResetEventSlim _done = new();
        private int _state = Pending;
        private T _result = default!;
        private ExceptionDispatchInfo? _error;

        public void Run()
        {
            if (Interlocked.CompareExchange(ref _state, Running, Pending) != Pending)
            {
                _done.Dispose();
                return;
            }

            try
            {
                _result = work();
            }
            catch (Exception e)
            {
                _error = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                _done.Set();
            }
        }

        public bool Wait(out T result)
        {
            // Withdrawn only if the main thread has not started it; once running it is waited for to the end.
            if (!_done.Wait(Timeout) && Interlocked.CompareExchange(ref _state, Withdrawn, Pending) == Pending)
            {
                result = default!;
                return false;
            }

            _done.Wait();
            _done.Dispose();
            _error?.Throw();
            result = _result;
            return true;
        }
    }
}
