using Augram.Core.Abstractions;

namespace Augram.Core.Tests.Sync.Support;

/// <summary>An in-memory <see cref="ISyncRepository"/> over a <see cref="SyncRemote"/>, with switchable failures.</summary>
internal sealed class FakeSyncRepository : ISyncRepository
{
    private readonly SyncRemote _remote;

    public FakeSyncRepository(SyncRemote remote)
    {
        _remote = remote;
    }

    public string Folder => "memory://clone";

    /// <summary>When set, Prepare and Pull fail with this line.</summary>
    public string? FailWith { get; set; }

    /// <summary>When set, Publish fails with this line and writes nothing.</summary>
    public string? FailPublishWith { get; set; }

    public string? PreparedUrl { get; private set; }

    public int Publishes { get; private set; }

    public SyncOperationResult Prepare(string repositoryUrl)
    {
        PreparedUrl = repositoryUrl;
        return Result(FailWith);
    }

    public SyncOperationResult Pull() => Result(FailWith);

    public IReadOnlyDictionary<string, string> ReadMachineFiles() => new Dictionary<string, string>(_remote.Files);

    public SyncOperationResult Publish(string machineId, string content, string message)
    {
        if (FailPublishWith is { } error)
        {
            return SyncOperationResult.Failed(error);
        }

        _remote.Files[machineId] = content;
        _remote.Messages.Add(message);
        Publishes++;
        return SyncOperationResult.Ok;
    }

    private static SyncOperationResult Result(string? error) => error is null ? SyncOperationResult.Ok : SyncOperationResult.Failed(error);
}
