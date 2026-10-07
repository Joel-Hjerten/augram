namespace Augram.Core.Tests.Sync.Support;

/// <summary>
/// The shared repo of a test: machine id → file text, what every machine's <see cref="FakeSyncRepository"/>
/// pulls from and publishes to, plus the commit messages. A test may also put a file here by hand (a broken one, say).
/// </summary>
internal sealed class SyncRemote
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Messages { get; } = [];
}
