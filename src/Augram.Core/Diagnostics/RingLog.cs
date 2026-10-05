namespace Augram.Core.Diagnostics;

/// <summary>
/// A fixed-capacity ring of the most recent items for a live view: one writer, any number of readers.
/// <see cref="Add"/> never allocates; <see cref="Snapshot"/> copies oldest-first into a fresh array.
/// <see cref="Version"/> increments on every change so a view can poll cheaply; <see cref="Changed"/> fires
/// on the writer's thread after the change, outside the lock, so a UI subscriber must marshal itself.
/// </summary>
public class RingLog<T>
{
    private readonly object _gate = new();
    private readonly T[] _items;
    private int _next;
    private int _count;
    private long _version;

    public RingLog(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _items = new T[capacity];
    }

    public event EventHandler? Changed;

    public int Capacity => _items.Length;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>Bumped on every add or clear; a reader that remembers the last value it saw knows whether to refresh.</summary>
    public long Version => Volatile.Read(ref _version);

    public void Add(T item)
    {
        lock (_gate)
        {
            _items[_next] = item;
            _next = (_next + 1) % _items.Length;
            if (_count < _items.Length)
            {
                _count++;
            }

            Interlocked.Increment(ref _version);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The retained items, oldest first.</summary>
    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate)
        {
            var result = new T[_count];
            var start = (_next - _count + _items.Length) % _items.Length;
            for (var i = 0; i < _count; i++)
            {
                result[i] = _items[(start + i) % _items.Length];
            }

            return result;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_items);
            _next = 0;
            _count = 0;
            Interlocked.Increment(ref _version);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
