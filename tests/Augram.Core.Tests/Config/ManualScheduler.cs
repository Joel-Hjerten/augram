namespace Augram.Core.Tests.Config;

/// <summary>A save scheduler the test ticks by hand; disposing a ticket cancels that action.</summary>
internal sealed class ManualScheduler
{
    private readonly List<Action> _pending = [];

    public int Scheduled { get; private set; }

    public int PendingCount => _pending.Count;

    public IDisposable Schedule(Action action)
    {
        Scheduled++;
        _pending.Add(action);
        return new Ticket(this, action);
    }

    /// <summary>Runs everything still pending, as the timer firing would.</summary>
    public void RunPending()
    {
        var due = _pending.ToArray();
        _pending.Clear();
        foreach (var action in due)
        {
            action();
        }
    }

    private sealed class Ticket : IDisposable
    {
        private readonly ManualScheduler _owner;
        private readonly Action _action;

        public Ticket(ManualScheduler owner, Action action)
        {
            _owner = owner;
            _action = action;
        }

        public void Dispose() => _owner._pending.Remove(_action);
    }
}
