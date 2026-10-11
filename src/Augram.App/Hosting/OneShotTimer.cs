namespace Augram.App.Hosting;

/// <summary>
/// Runs an action once after a delay: the config session's debounced save, sync's poll and change timers, stroke-button
/// detection's timeout. One <see cref="Timer"/> per request, no <c>DispatcherTimer</c>, so the same timer serves the app and
/// headless tests. The timer fires on a pool thread; <c>marshal</c> hands the action on (to the UI thread, which keeps
/// <c>ConfigSession</c> single-writer, Core/Config README). Disposing the result cancels it, and a request disposed after the
/// timer fired but before its marshalled action ran is skipped there too, so a burst of restarts (dispose, schedule again)
/// is still exactly one run.
/// </summary>
public sealed class OneShotTimer
{
    private readonly Action<Action> _marshal;

    /// <param name="marshal">Runs the action on the thread it belongs to; null runs it on the timer thread (sync, tests).</param>
    public OneShotTimer(Action<Action>? marshal = null)
    {
        _marshal = marshal ?? (action => action());
    }

    /// <summary>Runs <paramref name="action"/> once, <paramref name="delay"/> from now; disposing the result cancels it. To restart, dispose and schedule again.</summary>
    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(action);
        return new Pending(action, delay, _marshal);
    }

    private sealed class Pending : IDisposable
    {
        private readonly Timer _timer;
        private int _cancelled;

        public Pending(Action action, TimeSpan delay, Action<Action> marshal)
        {
            _timer = new Timer(_ => Fire(action, marshal));
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            Volatile.Write(ref _cancelled, 1);
            _timer.Dispose();
        }

        private void Fire(Action action, Action<Action> marshal)
        {
            if (Volatile.Read(ref _cancelled) != 0)
            {
                return;
            }

            marshal(() =>
            {
                if (Volatile.Read(ref _cancelled) == 0)
                {
                    action();
                }
            });
            _timer.Dispose();
        }
    }
}
