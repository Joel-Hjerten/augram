using System.Collections.Concurrent;

namespace Augram.TestSupport;

/// <summary>
/// The shared repo of a test: machine id → file text, what every machine's <see cref="FakeSyncRepository"/> pulls from and
/// publishes to, plus the commit messages. A test may also put a file here by hand (a broken one, say). Thread-safe: the
/// sync worker and the test thread both use it.
/// </summary>
public sealed class SyncRemote
{
    private readonly object _gate = new();
    private readonly List<string> _messages = [];

    /// <summary>The files, live; machine ids compare case-insensitively.</summary>
    public ConcurrentDictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The commit messages so far, oldest first (a snapshot).</summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_gate)
            {
                return [.. _messages];
            }
        }
    }

    /// <summary>A publish: the file and its commit message.</summary>
    public void Write(string machineId, string content, string message)
    {
        lock (_gate)
        {
            Files[machineId] = content;
            _messages.Add(message);
        }
    }
}
