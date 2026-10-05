namespace Augram.App.Hosting;

/// <summary>
/// The debounce behind <c>ConfigSession</c>'s live save: one <see cref="Timer"/> per request, no
/// <c>DispatcherTimer</c>, so the same scheduler serves the app and headless tests. The timer fires on a
/// pool thread; <c>marshal</c> hands the save to the UI thread, which keeps <c>ConfigSession</c>
/// single-writer (Core/Config README). A request disposed before its marshalled action ran is skipped
/// there too, so a burst of edits is still exactly one write.
/// </summary>
public sealed class TimerSaveScheduler
{
    private readonly TimeSpan _delay;
    private readonly Action<Action> _marshal;

    /// <param name="delay">How long after the request the action runs.</param>
    /// <param name="marshal">Runs the action on the writer thread; null runs it on the timer thread (tests).</param>
    public TimerSaveScheduler(TimeSpan delay, Action<Action>? marshal = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        _delay = delay;
        _marshal = marshal ?? (action => action());
    }

    /// <summary>Runs <paramref name="action"/> once, <c>delay</c> from now; disposing the result cancels it.</summary>
    public IDisposable Schedule(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new Pending(action, _delay, _marshal);
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
