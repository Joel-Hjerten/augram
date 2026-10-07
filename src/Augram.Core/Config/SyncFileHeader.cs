namespace Augram.Core.Config;

/// <summary>
/// The versions at the top of a sync file and its machine's name, read before anything else (README: format
/// version) so a file from a newer Augram is recognised as such and never half-read.
/// </summary>
public sealed record SyncFileHeader(int SchemaVersion, int FormatVersion, string? MachineName)
{
    /// <summary>True when this build cannot read the file: a newer config schema or a newer sync format.</summary>
    public bool IsNewer => SchemaVersion > ConfigDocument.CurrentSchemaVersion || FormatVersion > SyncFile.CurrentFormatVersion;
}
