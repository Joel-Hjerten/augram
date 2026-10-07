namespace Augram.App.Tests.Sync.Support;

/// <summary>
/// The shared repo of a test: machine id → file text, what every machine's <see cref="FakeSyncRepository"/> pulls
/// from and publishes to. Thread-safe: the sync worker and the test thread both use it.
/// </summary>
internal sealed class SyncRemote
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Files
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, string>(_files, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public void Write(string machineId, string content)
    {
        lock (_gate)
        {
            _files[machineId] = content;
        }
    }
}
