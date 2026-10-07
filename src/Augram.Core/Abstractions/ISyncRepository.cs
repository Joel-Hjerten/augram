namespace Augram.Core.Abstractions;

/// <summary>
/// The shared store machines sync through (F8 sync, ADR-0002 §2): a local clone of a private git repo
/// holding one file per machine under <c>machines/</c>. Implemented by <c>Augram.Sync.Git</c> over the
/// installed git; authentication is git's credential helper, never Augram's. Every member may block
/// for seconds (network) and is called from the sync worker only, never from the UI thread or a hook.
/// No member throws for an expected failure (offline, not signed in, rejected push); it returns it.
/// </summary>
public interface ISyncRepository
{
    /// <summary>The local clone's folder.</summary>
    string Folder { get; }

    /// <summary>Makes <see cref="Folder"/> a clone of <paramref name="repositoryUrl"/> (clones it when absent; an empty repo is fine). Refuses a folder that is a clone of another URL.</summary>
    SyncOperationResult Prepare(string repositoryUrl);

    /// <summary>Brings the clone up to date with the remote, keeping this machine's own unpushed commit on top.</summary>
    SyncOperationResult Pull();

    /// <summary>Every machine file in the clone as machine id → file text, this machine's own included.</summary>
    IReadOnlyDictionary<string, string> ReadMachineFiles();

    /// <summary>
    /// Writes <c>machines/&lt;machineId&gt;.json</c> and, when it changed, commits only that file with
    /// <paramref name="message"/> and pushes; a rejected push is pulled and retried once. Never force-pushes.
    /// </summary>
    SyncOperationResult Publish(string machineId, string content, string message);
}
