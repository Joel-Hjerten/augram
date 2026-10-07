using System.Globalization;
using Augram.Core.Config;

namespace Augram.Core.Sync;

/// <summary>
/// A machine whose file a newer Augram wrote (README: format version), or this machine when it has published in a
/// newer sync format than this build writes. Filled for <see cref="SyncStatus.NeedsUpdate"/>.
/// </summary>
public sealed record SyncNewerMachine(Guid MachineId, string MachineName, bool IsThisMachine, int FormatVersion, int SchemaVersion)
{
    /// <summary>"Mac's file is sync format 3; this build reads up to format 2."</summary>
    public string Description
    {
        get
        {
            var whose = IsThisMachine ? "This machine's last publish" : $"{MachineName}'s file";
            return SchemaVersion > ConfigDocument.CurrentSchemaVersion
                ? string.Create(CultureInfo.InvariantCulture, $"{whose} is config schema {SchemaVersion}; this build reads up to schema {ConfigDocument.CurrentSchemaVersion}.")
                : string.Create(CultureInfo.InvariantCulture, $"{whose} is sync format {FormatVersion}; this build reads up to format {SyncFile.CurrentFormatVersion}.");
        }
    }

    /// <summary>"Mac uses a newer Augram", "Mac and PC-WORK use a newer Augram", "this machine last synced with a newer Augram".</summary>
    public static string Summary(IReadOnlyList<SyncNewerMachine> machines)
    {
        ArgumentNullException.ThrowIfNull(machines);
        var others = machines.Where(machine => !machine.IsThisMachine).Select(machine => machine.MachineName).Distinct(StringComparer.Ordinal).ToArray();
        return others.Length switch
        {
            0 => "this machine last synced with a newer Augram",
            1 => $"{others[0]} uses a newer Augram",
            _ => $"{string.Join(", ", others[..^1])} and {others[^1]} use a newer Augram",
        };
    }
}
