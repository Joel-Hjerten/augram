using System.Text.Json.Serialization;

namespace Augram.Core.Config;

/// <summary>
/// Machine-to-machine sync (F8 sync), local to this machine and never synced itself. A null
/// <see cref="RepositoryUrl"/> is sync off. <see cref="MachineId"/> names this machine's file in the repo
/// (<c>machines/&lt;id&gt;.json</c>); it is <see cref="Guid.Empty"/> until first needed, then generated once
/// by <see cref="SettingsStore.EnsureMachineId"/> and kept. <see cref="MachineName"/> is what other machines
/// show ("from PC-HOME"); it defaults to the OS machine name. Rules live in <see cref="SyncSettingsRules"/>.
/// </summary>
/// <param name="RepositoryUrl">An <c>https://</c> URL, an scp-style <c>git@host:path</c>, or a local path; never with a user name or token in it.</param>
/// <param name="MachineId">Generated once; never edited by hand.</param>
/// <param name="MachineName">Blank or null in the file means the OS machine name.</param>
/// <param name="AutoSync">Sync at start, after a local change and every few minutes; off = only "Sync now".</param>
public sealed record SyncSettings(
    string? RepositoryUrl = null,
    Guid MachineId = default,
    string MachineName = "",
    bool AutoSync = true)
{
    public string MachineName { get; init; } = string.IsNullOrWhiteSpace(MachineName) ? Environment.MachineName : MachineName;

    /// <summary>True when a repository is set.</summary>
    [JsonIgnore]
    public bool IsOn => RepositoryUrl is not null;

    public static SyncSettings Default { get; } = new();
}
