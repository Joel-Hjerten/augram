using Augram.Core.Config;
using Augram.Core.Steps;

namespace Augram.Core.Sync;

/// <summary>
/// The machine files of one pull, read and sorted (README: the run): this machine's own file (only its revision
/// matters), the other machines' readable files in machine-id order, and one note per file skipped. A file is
/// skipped when it does not parse, breaks a rule, is filed under another machine's id, or would lose steps this
/// build cannot read (merging it would delete them everywhere; the note says to update Augram here). A file a newer
/// Augram wrote (README: format version) is not read past its header: it goes to <see cref="Newer"/>, and the run stops.
/// </summary>
internal sealed record SyncRepositoryFiles(SyncFile? Own, IReadOnlyList<SyncFile> Others, IReadOnlyList<string> Notes)
{
    /// <summary>The files a newer Augram wrote, this machine's own included; none of them is in <see cref="Own"/> or <see cref="Others"/>.</summary>
    public IReadOnlyList<SyncNewerMachine> Newer { get; init; } = [];

    public static SyncRepositoryFiles Read(IReadOnlyDictionary<string, string> texts, Guid self, StepRegistry steps)
    {
        SyncFile? own = null;
        var others = new List<SyncFile>();
        var notes = new List<string>();
        var newer = new List<SyncNewerMachine>();
        foreach (var (name, text) in texts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(name, out var id))
            {
                notes.Add($"machines/{name}.json skipped: the name is not a machine id.");
                continue;
            }

            if (SyncFileSerializer.ReadHeader(text) is { IsNewer: true } header)
            {
                newer.Add(new SyncNewerMachine(id, header.MachineName ?? $"machine {id}", id == self, header.FormatVersion, header.SchemaVersion));
                continue;
            }

            var dropped = new List<string>();
            if (!SyncFileSerializer.TryRead(text, steps, dropped.Add, out var file, out var error))
            {
                notes.Add(id == self ? $"This machine's own file could not be read and will be rewritten: {error}" : $"The file of machine {id} skipped: {error}");
            }
            else if (id == self)
            {
                own = file;
            }
            else if (file.MachineId != id)
            {
                notes.Add($"machines/{name}.json skipped: it holds the file of machine {file.MachineId}.");
            }
            else if (dropped.Count > 0)
            {
                notes.Add($"{file.MachineName}'s file skipped: it has steps this Augram cannot read ({dropped[0]}). Update Augram on this machine.");
            }
            else
            {
                others.Add(file);
            }
        }

        return new SyncRepositoryFiles(own, others, notes) { Newer = newer };
    }

    public IReadOnlyList<SyncMachineSummary> Summaries() => Others
        .Select(file => new SyncMachineSummary(
            file.MachineId,
            file.MachineName,
            file.WrittenAt,
            file.Gestures.Count,
            file.Mapping.Groups.Count,
            file.Mapping.AllCommands().Count()))
        .ToArray();
}
