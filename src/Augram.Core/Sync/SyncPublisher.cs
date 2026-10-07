using Augram.Core.Abstractions;
using Augram.Core.Config;

namespace Augram.Core.Sync;

/// <summary>
/// The last step of a run (README: the run): write this machine's file when there is something new to say, never
/// otherwise, so two idle machines do not commit every few minutes. Something new is: different items, different
/// <c>merged</c> entries (a new machine, a pending conflict or held key added or gone; a newer acknowledged revision
/// alone is not enough, or each machine's acknowledgement of the other's would publish forever), a new machine name,
/// the repo lacking this machine's newest revision, or a publish that failed last time. The revision and the sync format are
/// stored locally before it is published: another machine may acknowledge it even when the push looked failed here, and an
/// older build must never rewrite the file in an older format (README: format version).
/// </summary>
internal sealed class SyncPublisher
{
    private readonly ISyncRepository _repository;
    private readonly SyncBaseStore _bases;
    private readonly IClock _clock;

    public SyncPublisher(ISyncRepository repository, SyncBaseStore bases, IClock clock)
    {
        _repository = repository;
        _bases = bases;
        _clock = clock;
    }

    /// <summary>Returns an error line when publishing failed, else null (published or nothing to publish).</summary>
    public string? PublishIfNeeded(Guid self, string machineName, SyncFile? own, SyncPlanner.Plan plan, IEnumerable<SyncMachineState> states)
    {
        var merged = states.Select(state => state.Acknowledgement()).OfType<SyncAcknowledgement>().ToArray();
        var last = _bases.LastPublished();
        if (last is not null && !_bases.PublishPending && own?.Revision == last.Revision && own.MachineName == machineName
            && plan.Items.SameAs(last.Items) && SameEntries(last.Merged, merged))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var file = new SyncFile(ConfigDocument.CurrentSchemaVersion, self, machineName, now, plan.Gestures, plan.Mapping)
        {
            Revision = Guid.NewGuid(),
            Merged = merged,
        };
        _bases.Save(new SyncPublished(file.Revision, now, plan.Items.Contents(), merged));
        _bases.RaisePublishedFormatVersion(SyncFile.CurrentFormatVersion);
        var result = _repository.Publish(self.ToString("D"), SyncFileSerializer.Write(file), $"sync from {machineName}");
        _bases.PublishPending = !result.Succeeded;
        return result.Succeeded ? null : result.Error ?? "Publishing this machine's file failed.";
    }

    /// <summary>The same machines with the same excepted and pending keys; the acknowledged revisions may differ.</summary>
    private static bool SameEntries(IReadOnlyList<SyncAcknowledgement> before, IReadOnlyList<SyncAcknowledgement> after)
    {
        static string Shape(SyncAcknowledgement entry)
            => $"{entry.MachineId:D}|{string.Join(',', entry.Except.Order(StringComparer.Ordinal))}|{string.Join(',', entry.Pending.Order(StringComparer.Ordinal))}";

        return before.Select(Shape).Order(StringComparer.Ordinal).SequenceEqual(after.Select(Shape).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
