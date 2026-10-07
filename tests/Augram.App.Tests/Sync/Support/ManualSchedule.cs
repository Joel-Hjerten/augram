namespace Augram.App.Tests.Sync.Support;

/// <summary>
/// The sync service's timer seam without time: every scheduled action is recorded with its delay and runs only when
/// the test fires it; disposing an entry cancels it. Thread-safe (the worker schedules the poll).
/// </summary>
internal sealed class ManualSchedule
{
    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var entry = new Entry(delay, action);
        lock (_gate)
        {
            _entries.Add(entry);
        }

        return entry;
    }

    /// <summary>How many entries with this delay are waiting (scheduled, not cancelled, not fired).</summary>
    public int Pending(TimeSpan delay)
    {
        lock (_gate)
        {
            return _entries.Count(entry => entry.Delay == delay && entry.IsPending);
        }
    }

    /// <summary>How many entries with this delay were ever scheduled.</summary>
    public int Scheduled(TimeSpan delay)
    {
        lock (_gate)
        {
            return _entries.Count(entry => entry.Delay == delay);
        }
    }

    /// <summary>Runs every pending entry with this delay, as if its time had come; returns how many ran.</summary>
    public int Fire(TimeSpan delay)
    {
        List<Entry> due;
        lock (_gate)
        {
            due = [.. _entries.Where(entry => entry.Delay == delay && entry.IsPending)];
        }

        foreach (var entry in due)
        {
            entry.Run();
        }

        return due.Count;
    }

    private sealed class Entry : IDisposable
    {
        private readonly Action _action;
        private int _state; // 0 pending, 1 cancelled, 2 fired

        public Entry(TimeSpan delay, Action action)
        {
            Delay = delay;
            _action = action;
        }

        public TimeSpan Delay { get; }

        public bool IsPending => Volatile.Read(ref _state) == 0;

        public void Run()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
            {
                _action();
            }
        }

        public void Dispose() => Interlocked.CompareExchange(ref _state, 1, 0);
    }
}
