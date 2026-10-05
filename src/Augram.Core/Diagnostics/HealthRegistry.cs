using Augram.Core.Abstractions;

namespace Augram.Core.Diagnostics;

/// <summary>
/// The one <see cref="IHealthSource"/>. Components register a contributor that fills in the fields it
/// owns: <c>registry.Register(s => s with { HookAliveSince = _aliveSince })</c>. <see cref="Current"/>
/// folds every contributor over <see cref="HealthSnapshot.Empty"/>, later registrations winning on
/// a shared field. Registering costs one array copy; contributing costs nothing until a read, and reads
/// happen only when the Diagnostics tab refreshes. Contributors run on the reader's thread and must be
/// quick and non-throwing: they read volatile fields, they do not take the Engine's locks.
/// </summary>
public sealed class HealthRegistry : IHealthSource
{
    private readonly object _gate = new();
    private Func<HealthSnapshot, HealthSnapshot>[] _contributors = [];

    public int ContributorCount => Volatile.Read(ref _contributors).Length;

    /// <summary>Adds a contributor. Dispose the result to remove it (a reinstalled hook registers afresh).</summary>
    public IDisposable Register(Func<HealthSnapshot, HealthSnapshot> contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        lock (_gate)
        {
            _contributors = [.. _contributors, contributor];
        }

        return new Registration(this, contributor);
    }

    public HealthSnapshot Current()
    {
        var snapshot = HealthSnapshot.Empty;
        foreach (var contributor in Volatile.Read(ref _contributors))
        {
            snapshot = contributor(snapshot);
        }

        return snapshot;
    }

    private void Unregister(Func<HealthSnapshot, HealthSnapshot> contributor)
    {
        lock (_gate)
        {
            var index = Array.IndexOf(_contributors, contributor);
            if (index < 0)
            {
                return;
            }

            var next = new Func<HealthSnapshot, HealthSnapshot>[_contributors.Length - 1];
            Array.Copy(_contributors, 0, next, 0, index);
            Array.Copy(_contributors, index + 1, next, index, next.Length - index);
            _contributors = next;
        }
    }

    private sealed class Registration(HealthRegistry owner, Func<HealthSnapshot, HealthSnapshot> contributor) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unregister(contributor);
            }
        }
    }
}
